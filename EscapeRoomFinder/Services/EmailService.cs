using System.Net;
using System.Net.Mail;

namespace EscapeRoomFinder.Services
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

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUser = _configuration["Email:SmtpUser"];
                var smtpPass = _configuration["Email:SmtpPass"];
                var fromEmail = _configuration["Email:FromEmail"] ?? smtpUser;
                var fromName = _configuration["Email:FromName"] ?? "Escape Room Finder";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail!, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(to);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                throw;
            }
        }

        public async Task SendWelcomeEmailAsync(string to, string userName)
        {
            var subject = "Welcome to Escape Room Finder!";
            var body = $@"
                <h2>Welcome to Escape Room Finder, {userName}!</h2>
                <p>Thank you for joining our community of escape room enthusiasts.</p>
                <p>With your account, you can:</p>
                <ul>
                    <li>Save your favorite escape rooms</li>
                    <li>Leave reviews and ratings</li>
                    <li>Track your escape room adventures</li>
                    <li>Get personalized recommendations</li>
                </ul>
                <p>Start exploring escape rooms near you today!</p>
                <p>Best,<br/>The Escape Room Finder Team</p>
            ";

            await SendEmailAsync(to, subject, body);
        }

        public async Task SendClaimRequestConfirmationAsync(string to, string venueName, string planType)
        {
            var subject = $"Claim Request Received - {venueName}";
            var body = $@"
                <h2>Claim Request Confirmation</h2>
                <p>We've received your request to claim <strong>{venueName}</strong> on Escape Room Finder.</p>
                <p><strong>Plan:</strong> {planType}</p>
                <p>Next steps:</p>
                <ol>
                    <li>Complete the payment process</li>
                    <li>Verify your ownership</li>
                    <li>Start managing your listing</li>
                </ol>
                <p>If you have any questions, please reply to this email.</p>
                <p>Best,<br/>The Escape Room Finder Team</p>
            ";

            await SendEmailAsync(to, subject, body);
        }

        public async Task SendPaymentConfirmationAsync(string to, string venueName, decimal amount)
        {
            var subject = $"Payment Confirmed - {venueName}";
            var body = $@"
                <h2>Payment Confirmation</h2>
                <p>Your payment of <strong>${amount:F2}</strong> for <strong>{venueName}</strong> has been received.</p>
                <p>Your premium listing is now active. You can manage your listing from your dashboard.</p>
                <p>Premium features include:</p>
                <ul>
                    <li>Featured placement in search results</li>
                    <li>Photo gallery</li>
                    <li>Room showcase</li>
                    <li>Analytics dashboard</li>
                </ul>
                <p>Thank you for choosing Escape Room Finder!</p>
                <p>Best,<br/>The Escape Room Finder Team</p>
            ";

            await SendEmailAsync(to, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            var subject = "Reset Your Password - Escape Room Finder";
            var body = $@"
                <h2>Password Reset Request</h2>
                <p>We received a request to reset your password for your Escape Room Finder account.</p>
                <p>Click the link below to reset your password:</p>
                <p><a href=""{resetLink}"">Reset Password</a></p>
                <p>This link will expire in 24 hours.</p>
                <p>If you didn't request this, you can safely ignore this email.</p>
                <p>Best,<br/>The Escape Room Finder Team</p>
            ";

            await SendEmailAsync(to, subject, body);
        }
    }
}
