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

                return providers;
            }
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
                _ => null
            };
        }
    }
}
