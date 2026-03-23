using CookingClassFinder.Models.Domain;

namespace CookingClassFinder.Services
{
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl);
        Task<bool> HandleWebhookAsync(string payload, string signature);
        Task<ClaimRequest?> GetClaimRequestBySessionIdAsync(string sessionId);
    }

    public interface IPaymentServiceFactory
    {
        IPaymentService GetPaymentService(string provider);
    }
}
