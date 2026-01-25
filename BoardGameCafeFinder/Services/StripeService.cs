using Stripe;
using Stripe.Checkout;
using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Services
{
    public interface IStripeService
    {
        Task<Session> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl);
        Task<Session?> GetSessionAsync(string sessionId);
        Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId);
        Task<Stripe.Invoice?> GetStripeInvoiceAsync(string invoiceId);
        Stripe.Event ConstructWebhookEvent(string json, string signature, string webhookSecret);
    }

    public class StripeService : IStripeService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripeService> _logger;

        public StripeService(IConfiguration configuration, ILogger<StripeService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Configure Stripe API key
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        /// <summary>
        /// Creates a Stripe Checkout Session for the claim request
        /// </summary>
        public async Task<Session> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
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
                    { "cafe_id", claimRequest.CafeId.ToString() },
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
                            UnitAmount = (long)(claimRequest.TotalAmount * 100), // Stripe uses cents
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Premium Listing - {claimRequest.PlanType} Plan",
                                Description = $"{claimRequest.DurationMonths} month(s) of {claimRequest.PlanType} listing for your cafe"
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

        /// <summary>
        /// Retrieves a checkout session by ID
        /// </summary>
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

        /// <summary>
        /// Retrieves a payment intent by ID
        /// </summary>
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

        /// <summary>
        /// Retrieves a Stripe invoice by ID
        /// </summary>
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

        /// <summary>
        /// Constructs and validates a webhook event from Stripe
        /// </summary>
        public Stripe.Event ConstructWebhookEvent(string json, string signature, string webhookSecret)
        {
            return EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
    }
}
