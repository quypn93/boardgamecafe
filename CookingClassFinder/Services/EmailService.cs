using System.Net;
using System.Net.Mail;

namespace CookingClassFinder.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "localhost";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "25");
                var smtpUser = _configuration["Email:SmtpUser"];
                var smtpPass = _configuration["Email:SmtpPass"];
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@cookingclassfinder.com";
                var fromName = _configuration["Email:FromName"] ?? "Cooking Class Finder";

                using var client = new SmtpClient(smtpHost, smtpPort);
                if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
                {
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    client.EnableSsl = true;
                }

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                message.To.Add(to);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
            }
        }

        public async Task SendWelcomeEmailAsync(string to, string displayName)
        {
            var subject = "Welcome to Cooking Class Finder!";
            var body = $@"
                <h1>Welcome, {displayName}!</h1>
                <p>Thank you for joining Cooking Class Finder.</p>
                <p>Discover amazing cooking classes near you and start your culinary journey today!</p>
            ";
            await SendEmailAsync(to, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            var subject = "Reset Your Password - Cooking Class Finder";
            var body = $@"
                <h1>Password Reset Request</h1>
                <p>Click the link below to reset your password:</p>
                <p><a href='{resetLink}'>{resetLink}</a></p>
                <p>If you didn't request this, please ignore this email.</p>
            ";
            await SendEmailAsync(to, subject, body);
        }
    }
}
