
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PharmacyAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string body)
        {
            // =====================================================
            // LOAD CONFIGURATION
            // =====================================================

            var host = _configuration["EmailSettings:SmtpHost"];
            var portValue = _configuration["EmailSettings:SmtpPort"];
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];

            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException(
                    "EmailSettings:SmtpHost is missing.");

            if (!int.TryParse(portValue, out var port))
                throw new InvalidOperationException(
                    "EmailSettings:SmtpPort is missing or invalid.");

            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException(
                    "EmailSettings:Username is missing.");

            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "EmailSettings:Password is missing.");

            if (string.IsNullOrWhiteSpace(senderEmail))
                throw new InvalidOperationException(
                    "EmailSettings:SenderEmail is missing.");

            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException(
                    "Recipient email address is required.",
                    nameof(to));

            if (!MailboxAddress.TryParse(to, out var recipient))
                throw new ArgumentException(
                    "Recipient email address is invalid.",
                    nameof(to));

            // =====================================================
            // BUILD EMAIL
            // =====================================================

            var email = new MimeMessage();

            email.From.Add(
                new MailboxAddress(
                    string.IsNullOrWhiteSpace(senderName)
                        ? "Lily Store"
                        : senderName,
                    senderEmail));

            email.To.Add(recipient);
            email.Subject = subject ?? string.Empty;

            email.Body = new TextPart("html")
            {
                Text = body ?? string.Empty
            };

            // =====================================================
            // SMTP CONNECTION AND SENDING
            // =====================================================

            using var smtp = new SmtpClient();

            smtp.Timeout = 20000;

            try
            {
                _logger.LogInformation(
                    "Connecting to SMTP host {Host} on port {Port}.",
                    host,
                    port);

                var securityOption = port switch
                {
                    465 => SecureSocketOptions.SslOnConnect,
                    587 => SecureSocketOptions.StartTls,
                    _ => SecureSocketOptions.Auto
                };

                await smtp.ConnectAsync(
                    host,
                    port,
                    securityOption);

                await smtp.AuthenticateAsync(
                    username,
                    password);

                await smtp.SendAsync(email);

                _logger.LogInformation(
                    "Email sent successfully to {Recipient}.",
                    to);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Recipient}.",
                    to);

                throw;
            }
            finally
            {
                if (smtp.IsConnected)
                {
                    try
                    {
                        await smtp.DisconnectAsync(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "SMTP disconnection failed.");
                    }
                }
            }
        }
    }
}
