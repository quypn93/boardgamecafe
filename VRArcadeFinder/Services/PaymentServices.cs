using Stripe;
using Stripe.Checkout;
using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Services
{
    /// <summary>
    /// Payment session result from any payment provider
    /// </summary>
    public class PaymentSessionResult
    {
        public bool Success { get; set; }
        public string? SessionId { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Payment session details
    /// </summary>
    public class PaymentSessionDetails
    {
        public string SessionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? PaymentId { get; set; }
        public string? PayerId { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? Currency { get; set; }
        public string? CustomerEmail { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Webhook event result
    /// </summary>
    public class WebhookEventResult
    {
        public bool IsValid { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? PaymentId { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Abstracted payment service interface for multiple providers
    /// </summary>
    public interface IPaymentService
    {
        string ProviderName { get; }
        Task<PaymentSessionResult> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl);
        Task<PaymentSessionDetails?> GetSessionAsync(string sessionId);
        Task<bool> CapturePaymentAsync(string sessionId, string? payerId = null);
        WebhookEventResult ParseWebhookEvent(string payload, string? signature, string? webhookSecret);
        bool IsConfigured();
    }

    /// <summary>
    /// Legacy interface for backward compatibility
    /// </summary>
    public interface IStripeService
    {
        Task<Session> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl);
        Task<Session?> GetSessionAsync(string sessionId);
        Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId);
        Task<Stripe.Invoice?> GetStripeInvoiceAsync(string invoiceId);
        Stripe.Event ConstructWebhookEvent(string json, string signature, string webhookSecret);
    }

    /// <summary>
    /// Stripe payment service implementing both legacy and new interfaces
    /// </summary>
    public class StripePaymentService : IPaymentService, IStripeService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripePaymentService> _logger;

        public string ProviderName => "Stripe";

        public StripePaymentService(IConfiguration configuration, ILogger<StripePaymentService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var secretKey = _configuration["Stripe:SecretKey"];
            if (!string.IsNullOrEmpty(secretKey))
            {
                StripeConfiguration.ApiKey = secretKey;
            }
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_configuration["Stripe:SecretKey"]) &&
                   !string.IsNullOrEmpty(_configuration["Stripe:PublishableKey"]);
        }

        public async Task<PaymentSessionResult> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
        {
            try
            {
                var session = await CreateStripeCheckoutSessionAsync(claimRequest, successUrl, cancelUrl);
                return new PaymentSessionResult
                {
                    Success = true,
                    SessionId = session.Id,
                    CheckoutUrl = session.Url
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error creating Stripe checkout session");
                return new PaymentSessionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        async Task<PaymentSessionDetails?> IPaymentService.GetSessionAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null) return null;

            return new PaymentSessionDetails
            {
                SessionId = session.Id,
                Status = MapStripeStatus(session.PaymentStatus),
                PaymentId = session.PaymentIntentId,
                AmountPaid = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : null,
                Currency = session.Currency,
                CustomerEmail = session.CustomerEmail,
                Metadata = session.Metadata ?? new Dictionary<string, string>()
            };
        }

        public Task<bool> CapturePaymentAsync(string sessionId, string? payerId = null)
        {
            return Task.FromResult(true);
        }

        public WebhookEventResult ParseWebhookEvent(string payload, string? signature, string? webhookSecret)
        {
            try
            {
                if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(webhookSecret))
                {
                    return new WebhookEventResult { IsValid = false, ErrorMessage = "Missing signature or webhook secret" };
                }

                var stripeEvent = ConstructWebhookEvent(payload, signature, webhookSecret);

                var result = new WebhookEventResult
                {
                    IsValid = true,
                    EventType = stripeEvent.Type
                };

                if (stripeEvent.Data.Object is Session session)
                {
                    result.SessionId = session.Id;
                    result.PaymentId = session.PaymentIntentId;
                    result.Metadata = session.Metadata ?? new Dictionary<string, string>();
                }
                else if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
                {
                    result.PaymentId = paymentIntent.Id;
                    result.Metadata = paymentIntent.Metadata ?? new Dictionary<string, string>();
                }

                return result;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error parsing Stripe webhook");
                return new WebhookEventResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }

        private string MapStripeStatus(string? stripeStatus)
        {
            return stripeStatus?.ToLower() switch
            {
                "paid" => "completed",
                "unpaid" => "pending",
                "no_payment_required" => "completed",
                _ => "pending"
            };
        }

        public async Task<Session> CreateStripeCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerEmail = claimRequest.ContactEmail,
                ClientReferenceId = claimRequest.ClaimRequestId.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    { "claim_request_id", claimRequest.ClaimRequestId.ToString() },
                    { "arcade_id", claimRequest.ArcadeId.ToString() },
                    { "plan_type", claimRequest.PlanType },
                    { "duration_months", claimRequest.DurationMonths.ToString() }
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = (long)(claimRequest.TotalAmount * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Premium Listing - {claimRequest.PlanType} Plan",
                                Description = $"{claimRequest.DurationMonths} month(s) of {claimRequest.PlanType} listing for your VR arcade"
                            }
                        },
                        Quantity = 1
                    }
                },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "claim_request_id", claimRequest.ClaimRequestId.ToString() }
                    }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation("Created Stripe checkout session {SessionId} for ClaimRequest {ClaimRequestId}",
                session.Id, claimRequest.ClaimRequestId);

            return session;
        }

        Task<Session> IStripeService.CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
        {
            return CreateStripeCheckoutSessionAsync(claimRequest, successUrl, cancelUrl);
        }

        public async Task<Session?> GetSessionAsync(string sessionId)
        {
            try
            {
                var service = new SessionService();
                return await service.GetAsync(sessionId);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error retrieving Stripe session {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                return await service.GetAsync(paymentIntentId);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error retrieving PaymentIntent {PaymentIntentId}", paymentIntentId);
                return null;
            }
        }

        public async Task<Stripe.Invoice?> GetStripeInvoiceAsync(string invoiceId)
        {
            try
            {
                var service = new InvoiceService();
                return await service.GetAsync(invoiceId);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error retrieving Stripe invoice {InvoiceId}", invoiceId);
                return null;
            }
        }

        public Stripe.Event ConstructWebhookEvent(string json, string signature, string webhookSecret)
        {
            return EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
    }

    /// <summary>
    /// Factory for creating payment service instances
    /// </summary>
    public interface IPaymentServiceFactory
    {
        IPaymentService GetPaymentService(string? provider = null);
    }

    public class PaymentServiceFactory : IPaymentServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly StripePaymentService _stripeService;

        public PaymentServiceFactory(
            IConfiguration configuration,
            StripePaymentService stripeService)
        {
            _configuration = configuration;
            _stripeService = stripeService;
        }

        public IPaymentService GetPaymentService(string? provider = null)
        {
            var defaultProvider = _configuration["Payment:DefaultProvider"] ?? "stripe";
            provider ??= defaultProvider;

            return provider.ToLower() switch
            {
                "stripe" => _stripeService,
                _ => _stripeService
            };
        }
    }
}
