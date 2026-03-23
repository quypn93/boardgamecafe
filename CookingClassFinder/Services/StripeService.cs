using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace CookingClassFinder.Services
{
    public class StripeService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripeService> _logger;

        public StripeService(ApplicationDbContext context, IConfiguration configuration, ILogger<StripeService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        public async Task<string> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(claimRequest.TotalAmount * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"{claimRequest.PlanType} Plan - {claimRequest.DurationMonths} month(s)",
                                Description = $"Premium listing for cooking school"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerEmail = claimRequest.ContactEmail,
                Metadata = new Dictionary<string, string>
                {
                    { "claim_request_id", claimRequest.ClaimRequestId.ToString() }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            claimRequest.StripeSessionId = session.Id;
            claimRequest.PaymentStatus = "processing";
            await _context.SaveChangesAsync();

            return session.Url;
        }

        public async Task<bool> HandleWebhookAsync(string payload, string signature)
        {
            try
            {
                var webhookSecret = _configuration["Stripe:WebhookSecret"];
                var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session?.Metadata.TryGetValue("claim_request_id", out var claimIdStr) == true &&
                        int.TryParse(claimIdStr, out var claimId))
                    {
                        var claimRequest = await _context.ClaimRequests.FindAsync(claimId);
                        if (claimRequest != null)
                        {
                            claimRequest.PaymentStatus = "completed";
                            claimRequest.StripePaymentIntentId = session.PaymentIntentId;
                            claimRequest.PaidAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe webhook handling failed");
                return false;
            }
        }

        public async Task<ClaimRequest?> GetClaimRequestBySessionIdAsync(string sessionId)
        {
            return await _context.ClaimRequests
                .Include(c => c.School)
                .FirstOrDefaultAsync(c => c.StripeSessionId == sessionId);
        }
    }
}
