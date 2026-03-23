using System.Globalization;

namespace BoardGameCafeFinder.Services
{
    /// <summary>
    /// Factory for creating payment service based on configuration
    /// </summary>
    public interface IPaymentServiceFactory
    {
        /// <summary>
        /// Get the active payment service based on configuration
        /// </summary>
        IPaymentService GetPaymentService();

        /// <summary>
        /// Get a specific payment service by provider name
        /// </summary>
        IPaymentService? GetPaymentService(string providerName);

        /// <summary>
        /// Get the payment service based on user's country/culture.
        /// Non-Vietnam users → LemonSqueezy, Vietnam users → configured provider (Stripe/PayPal)
        /// </summary>
        IPaymentService GetPaymentServiceForUser();

        /// <summary>
        /// Check if the current user should use LemonSqueezy (non-Vietnam)
        /// </summary>
        bool ShouldUseLemonSqueezy();

        /// <summary>
        /// Get the name of the active provider
        /// </summary>
        string ActiveProvider { get; }

        /// <summary>
        /// Get list of available providers
        /// </summary>
        IEnumerable<string> AvailableProviders { get; }
    }

    public class PaymentServiceFactory : IPaymentServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentServiceFactory> _logger;

        public PaymentServiceFactory(
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILogger<PaymentServiceFactory> logger)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public string ActiveProvider => _configuration["Payment:ActiveProvider"] ?? "PayPal";

        public IEnumerable<string> AvailableProviders
        {
            get
            {
                var providers = new List<string>();

                var stripeService = _serviceProvider.GetService<StripePaymentService>();
                if (stripeService?.IsConfigured() == true)
                    providers.Add("Stripe");

                var paypalService = _serviceProvider.GetService<PayPalPaymentService>();
                if (paypalService?.IsConfigured() == true)
                    providers.Add("PayPal");

                var lemonSqueezyService = _serviceProvider.GetService<LemonSqueezyPaymentService>();
                if (lemonSqueezyService?.IsConfigured() == true)
                    providers.Add("LemonSqueezy");

                return providers;
            }
        }

        public bool ShouldUseLemonSqueezy()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return culture != "vi";
        }

        public IPaymentService GetPaymentServiceForUser()
        {
            if (ShouldUseLemonSqueezy())
            {
                var lemonSqueezy = _serviceProvider.GetService<LemonSqueezyPaymentService>();
                if (lemonSqueezy?.IsConfigured() == true)
                {
                    _logger.LogDebug("Using LemonSqueezy for non-Vietnam user (culture: {Culture})",
                        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                    return lemonSqueezy;
                }

                _logger.LogWarning("LemonSqueezy not configured, falling back to default provider");
            }

            // Vietnam users or LemonSqueezy not configured → use default provider
            return GetPaymentService();
        }

        public IPaymentService GetPaymentService()
        {
            var provider = ActiveProvider;
            _logger.LogDebug("Getting payment service for provider: {Provider}", provider);

            var service = GetPaymentService(provider);
            if (service == null)
            {
                _logger.LogWarning("Configured provider {Provider} not available, falling back", provider);
                // Try fallback
                service = _serviceProvider.GetService<PayPalPaymentService>() as IPaymentService
                       ?? _serviceProvider.GetService<StripePaymentService>() as IPaymentService;
            }

            if (service == null)
            {
                throw new InvalidOperationException("No payment service is configured. Please configure either Stripe or PayPal.");
            }

            return service;
        }

        public IPaymentService? GetPaymentService(string providerName)
        {
            return providerName?.ToLower() switch
            {
                "stripe" => _serviceProvider.GetService<StripePaymentService>(),
                "paypal" => _serviceProvider.GetService<PayPalPaymentService>(),
                "lemonsqueezy" => _serviceProvider.GetService<LemonSqueezyPaymentService>(),
                _ => null
            };
        }
    }
}
