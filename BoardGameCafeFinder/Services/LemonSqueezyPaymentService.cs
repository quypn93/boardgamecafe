using BoardGameCafeFinder.Models.Domain;
using System.Text.Json;

namespace BoardGameCafeFinder.Services
{
    public class LemonSqueezyPaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LemonSqueezyPaymentService> _logger;

        public string ProviderName => "LemonSqueezy";

        public LemonSqueezyPaymentService(IConfiguration configuration, ILogger<LemonSqueezyPaymentService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsConfigured()
        {
            var apiKey = _configuration["LemonSqueezy:ApiKey"];
            var checkoutUrls = _configuration.GetSection("LemonSqueezy:CheckoutUrls");
            return !string.IsNullOrEmpty(apiKey) &&
                   apiKey != "YOUR_LEMONSQUEEZY_API_KEY" &&
                   checkoutUrls.GetChildren().Any(c => !string.IsNullOrEmpty(c.Value) && c.Value != "YOUR_LEMONSQUEEZY_CHECKOUT_URL");
        }

        public Task<PaymentSessionResult> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
        {
            try
            {
                // Build the checkout URL key: e.g. "Premium_3"
                var urlKey = $"{claimRequest.PlanType}_{claimRequest.DurationMonths}";
                var baseCheckoutUrl = _configuration[$"LemonSqueezy:CheckoutUrls:{urlKey}"];

                if (string.IsNullOrEmpty(baseCheckoutUrl) || baseCheckoutUrl == "YOUR_LEMONSQUEEZY_CHECKOUT_URL")
                {
                    _logger.LogError("LemonSqueezy checkout URL not configured for {UrlKey}", urlKey);
                    return Task.FromResult(new PaymentSessionResult
                    {
                        Success = false,
                        ErrorMessage = $"Checkout URL not configured for {claimRequest.PlanType} plan with {claimRequest.DurationMonths} month(s) duration."
                    });
                }

                // Build checkout URL with query parameters
                // LemonSqueezy supports pre-filling checkout fields via URL params
                var separator = baseCheckoutUrl.Contains('?') ? "&" : "?";
                var checkoutUrl = $"{baseCheckoutUrl}{separator}" +
                    $"checkout[email]={Uri.EscapeDataString(claimRequest.ContactEmail)}" +
                    $"&checkout[name]={Uri.EscapeDataString(claimRequest.ContactName)}" +
                    $"&checkout[custom][claim_request_id]={claimRequest.ClaimRequestId}" +
                    $"&checkout[custom][cafe_id]={claimRequest.CafeId}" +
                    $"&checkout[custom][plan_type]={Uri.EscapeDataString(claimRequest.PlanType)}" +
                    $"&checkout[custom][duration_months]={claimRequest.DurationMonths}";

                // Use claim request ID as session identifier
                var sessionId = $"ls_{claimRequest.ClaimRequestId}_{DateTime.UtcNow.Ticks}";

                _logger.LogInformation("Created LemonSqueezy checkout for ClaimRequest {ClaimId}, Plan: {Plan}_{Duration}",
                    claimRequest.ClaimRequestId, claimRequest.PlanType, claimRequest.DurationMonths);

                return Task.FromResult(new PaymentSessionResult
                {
                    Success = true,
                    SessionId = sessionId,
                    CheckoutUrl = checkoutUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating LemonSqueezy checkout session");
                return Task.FromResult(new PaymentSessionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public Task<PaymentSessionDetails?> GetSessionAsync(string sessionId)
        {
            // LemonSqueezy uses webhooks for payment confirmation
            // When called from CheckoutSuccess, we return a pending status
            // The actual completion is handled via webhook
            return Task.FromResult<PaymentSessionDetails?>(new PaymentSessionDetails
            {
                SessionId = sessionId,
                Status = "pending",
                Metadata = new Dictionary<string, string>
                {
                    { "provider", "LemonSqueezy" }
                }
            });
        }

        public Task<bool> CapturePaymentAsync(string sessionId, string? payerId = null)
        {
            // LemonSqueezy handles capture automatically - no manual capture needed
            return Task.FromResult(true);
        }

        public WebhookEventResult ParseWebhookEvent(string payload, string? signature, string? webhookSecret)
        {
            try
            {
                // Verify webhook signature if secret is configured
                if (!string.IsNullOrEmpty(webhookSecret) && !string.IsNullOrEmpty(signature))
                {
                    var isValid = VerifyWebhookSignature(payload, signature, webhookSecret);
                    if (!isValid)
                    {
                        _logger.LogWarning("Invalid LemonSqueezy webhook signature");
                        return new WebhookEventResult { IsValid = false, ErrorMessage = "Invalid webhook signature" };
                    }
                }

                var json = JsonDocument.Parse(payload);
                var root = json.RootElement;

                var meta = root.GetProperty("meta");
                var eventName = meta.GetProperty("event_name").GetString() ?? "";

                var data = root.GetProperty("data");
                var attributes = data.GetProperty("attributes");

                var result = new WebhookEventResult
                {
                    IsValid = true,
                    EventType = eventName
                };

                // Extract order/subscription ID
                if (data.TryGetProperty("id", out var idElement))
                {
                    result.SessionId = idElement.GetString();
                }

                // Extract payment ID
                if (attributes.TryGetProperty("identifier", out var identifierElement))
                {
                    result.PaymentId = identifierElement.GetString();
                }

                // Extract custom data (claim_request_id, cafe_id, etc.)
                if (meta.TryGetProperty("custom_data", out var customData))
                {
                    if (customData.TryGetProperty("claim_request_id", out var claimId))
                    {
                        result.Metadata["claim_request_id"] = claimId.GetString() ?? "";
                    }
                    if (customData.TryGetProperty("cafe_id", out var cafeId))
                    {
                        result.Metadata["cafe_id"] = cafeId.GetString() ?? "";
                    }
                    if (customData.TryGetProperty("plan_type", out var planType))
                    {
                        result.Metadata["plan_type"] = planType.GetString() ?? "";
                    }
                    if (customData.TryGetProperty("duration_months", out var duration))
                    {
                        result.Metadata["duration_months"] = duration.GetString() ?? "";
                    }
                }

                // Extract amount
                if (attributes.TryGetProperty("total", out var totalElement))
                {
                    result.Metadata["total"] = totalElement.GetInt32().ToString();
                }

                if (attributes.TryGetProperty("status", out var statusElement))
                {
                    result.Metadata["status"] = statusElement.GetString() ?? "";
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LemonSqueezy webhook");
                return new WebhookEventResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }

        private bool VerifyWebhookSignature(string payload, string signature, string secret)
        {
            try
            {
                using var hmac = new System.Security.Cryptography.HMACSHA256(
                    System.Text.Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
                var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return string.Equals(computedSignature, signature, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying LemonSqueezy webhook signature");
                return false;
            }
        }
    }
}
