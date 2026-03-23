namespace CookingClassFinder.Services
{
    public class PaymentServiceFactory : IPaymentServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public PaymentServiceFactory(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public IPaymentService GetPaymentService(string? provider = null)
        {
            provider ??= _configuration["Payment:ActiveProvider"] ?? "stripe";

            return provider.ToLower() switch
            {
                "stripe" => _serviceProvider.GetRequiredService<StripeService>(),
                _ => _serviceProvider.GetRequiredService<StripeService>()
            };
        }
    }
}
