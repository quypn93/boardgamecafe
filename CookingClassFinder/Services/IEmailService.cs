namespace CookingClassFinder.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendWelcomeEmailAsync(string to, string displayName);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
    }
}
