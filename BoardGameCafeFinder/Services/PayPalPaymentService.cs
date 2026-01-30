using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using BoardGameCafeFinder.Models.Domain;
using PayPalHttp;

namespace BoardGameCafeFinder.Services
{
    public class PayPalPaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalPaymentService> _logger;
        private readonly PayPalHttpClient _client;

        public string ProviderName => "PayPal";

        public PayPalPaymentService(IConfiguration configuration, ILogger<PayPalPaymentService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Initialize PayPal client
            var clientId = _configuration["PayPal:ClientId"];
            var clientSecret = _configuration["PayPal:ClientSecret"];
            var environment = _configuration["PayPal:Environment"]?.ToLower() ?? "sandbox";

            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            {
                PayPalEnvironment env = environment == "live"
                    ? new LiveEnvironment(clientId, clientSecret)
                    : new SandboxEnvironment(clientId, clientSecret);

                _client = new PayPalHttpClient(env);
            }
            else
            {
                // Create a dummy client for when not configured
                _client = new PayPalHttpClient(new SandboxEnvironment("dummy", "dummy"));
            }
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_configuration["PayPal:ClientId"]) &&
                   !string.IsNullOrEmpty(_configuration["PayPal:ClientSecret"]);
        }

        public async Task<PaymentSessionResult> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
        {
            try
            {
                // Append order ID placeholder to success URL for PayPal
                // PayPal will redirect to: successUrl?token=ORDER_ID&PayerID=PAYER_ID
                var order = new OrderRequest()
                {
                    CheckoutPaymentIntent = "CAPTURE",
                    PurchaseUnits = new List<PurchaseUnitRequest>
                    {
                        new PurchaseUnitRequest
                        {
                            ReferenceId = claimRequest.ClaimRequestId.ToString(),
                            Description = $"Premium Listing - {claimRequest.PlanType} Plan ({claimRequest.DurationMonths} months)",
                            CustomId = claimRequest.ClaimRequestId.ToString(),
                            AmountWithBreakdown = new AmountWithBreakdown
                            {
                                CurrencyCode = "USD",
                                Value = claimRequest.TotalAmount.ToString("0.00")
                            }
                        }
                    },
                    ApplicationContext = new ApplicationContext
                    {
                        BrandName = "Board Game Cafe Finder",
                        LandingPage = "BILLING",
                        UserAction = "PAY_NOW",
                        ReturnUrl = successUrl,
                        CancelUrl = cancelUrl
                    }
                };

                var request = new OrdersCreateRequest();
                request.Prefer("return=representation");
                request.RequestBody(order);

                var response = await _client.Execute(request);
                var result = response.Result<Order>();

                // Find the approval link
                var approvalLink = result.Links.FirstOrDefault(l => l.Rel == "approve")?.Href;

                _logger.LogInformation("Created PayPal order {OrderId} for ClaimRequest {ClaimRequestId}",
                    result.Id, claimRequest.ClaimRequestId);

                return new PaymentSessionResult
                {
                    Success = true,
                    SessionId = result.Id,
                    CheckoutUrl = approvalLink
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PayPal order");
                return new PaymentSessionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PaymentSessionDetails?> GetSessionAsync(string sessionId)
        {
            try
            {
                var request = new OrdersGetRequest(sessionId);
                var response = await _client.Execute(request);
                var order = response.Result<Order>();

                return new PaymentSessionDetails
                {
                    SessionId = order.Id,
                    Status = MapPayPalStatus(order.Status),
                    PaymentId = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault()?.Id,
                    AmountPaid = decimal.TryParse(order.PurchaseUnits?.FirstOrDefault()?.AmountWithBreakdown?.Value, out var amt) ? amt : null,
                    Currency = order.PurchaseUnits?.FirstOrDefault()?.AmountWithBreakdown?.CurrencyCode,
                    CustomerEmail = order.Payer?.Email,
                    Metadata = new Dictionary<string, string>
                    {
                        { "claim_request_id", order.PurchaseUnits?.FirstOrDefault()?.CustomId ?? "" },
                        { "reference_id", order.PurchaseUnits?.FirstOrDefault()?.ReferenceId ?? "" }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PayPal order {OrderId}", sessionId);
                return null;
            }
        }

        public async Task<bool> CapturePaymentAsync(string sessionId, string? payerId = null)
        {
            try
            {
                var request = new OrdersCaptureRequest(sessionId);
                request.RequestBody(new OrderActionRequest());

                var response = await _client.Execute(request);
                var order = response.Result<Order>();

                _logger.LogInformation("Captured PayPal payment for order {OrderId}, Status: {Status}",
                    order.Id, order.Status);

                return order.Status == "COMPLETED";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing PayPal payment for order {OrderId}", sessionId);
                return false;
            }
        }

        public WebhookEventResult ParseWebhookEvent(string payload, string? signature, string? webhookSecret)
        {
            try
            {
                // PayPal webhooks use different verification mechanism
                // For simplicity, we'll parse the JSON directly
                // In production, you should verify the webhook signature using PayPal's verification API

                var json = System.Text.Json.JsonDocument.Parse(payload);
                var root = json.RootElement;

                var eventType = root.GetProperty("event_type").GetString() ?? "";
                var resource = root.GetProperty("resource");

                var result = new WebhookEventResult
                {
                    IsValid = true,
                    EventType = eventType
                };

                // Extract order/payment info
                if (resource.TryGetProperty("id", out var idElement))
                {
                    result.SessionId = idElement.GetString();
                }

                if (resource.TryGetProperty("custom_id", out var customIdElement))
                {
                    result.Metadata["claim_request_id"] = customIdElement.GetString() ?? "";
                }

                if (resource.TryGetProperty("purchase_units", out var purchaseUnits))
                {
                    var firstUnit = purchaseUnits.EnumerateArray().FirstOrDefault();
                    if (firstUnit.TryGetProperty("custom_id", out var customId))
                    {
                        result.Metadata["claim_request_id"] = customId.GetString() ?? "";
                    }
                    if (firstUnit.TryGetProperty("reference_id", out var refId))
                    {
                        result.Metadata["reference_id"] = refId.GetString() ?? "";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PayPal webhook");
                return new WebhookEventResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }

        private string MapPayPalStatus(string? paypalStatus)
        {
            return paypalStatus?.ToUpper() switch
            {
                "COMPLETED" => "completed",
                "APPROVED" => "pending", // Approved but not captured yet
                "CREATED" => "pending",
                "SAVED" => "pending",
                "VOIDED" => "cancelled",
                "PAYER_ACTION_REQUIRED" => "pending",
                _ => "pending"
            };
        }
    }
}
