using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Services
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
        public string Status { get; set; } = string.Empty; // "pending", "completed", "failed", "cancelled"
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
        /// <summary>
        /// Get the provider name (e.g., "Stripe", "PayPal")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Creates a checkout session for the claim request
        /// </summary>
        Task<PaymentSessionResult> CreateCheckoutSessionAsync(ClaimRequest claimRequest, string successUrl, string cancelUrl);

        /// <summary>
        /// Gets the session details by ID
        /// </summary>
        Task<PaymentSessionDetails?> GetSessionAsync(string sessionId);

        /// <summary>
        /// Captures/executes a payment after approval (for PayPal)
        /// For Stripe this may be a no-op as payment is captured automatically
        /// </summary>
        Task<bool> CapturePaymentAsync(string sessionId, string? payerId = null);

        /// <summary>
        /// Validates and parses a webhook event
        /// </summary>
        WebhookEventResult ParseWebhookEvent(string payload, string? signature, string? webhookSecret);

        /// <summary>
        /// Check if provider is properly configured
        /// </summary>
        bool IsConfigured();
    }
}
