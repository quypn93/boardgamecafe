namespace EscapeRoomFinder.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
        Task SendWelcomeEmailAsync(string to, string userName);
        Task SendClaimRequestConfirmationAsync(string to, string venueName, string planType);
        Task SendPaymentConfirmationAsync(string to, string venueName, decimal amount);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
    }
}
