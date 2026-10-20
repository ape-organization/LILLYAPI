
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MailKit;
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
        private readonly HttpClient _httpClient;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string body)
        {
            var apiToken =
                _configuration["HostingerMail:ApiToken"];

            var mailboxResourceId =
                _configuration["HostingerMail:MailboxResourceId"];

            var senderEmail =
                _configuration["HostingerMail:SenderEmail"];

            var senderName =
                _configuration["HostingerMail:SenderName"];

            if (string.IsNullOrWhiteSpace(apiToken))
                throw new InvalidOperationException(
                    "HostingerMail:ApiToken is missing.");

            if (string.IsNullOrWhiteSpace(mailboxResourceId))
                throw new InvalidOperationException(
                    "HostingerMail:MailboxResourceId is missing.");

            if (string.IsNullOrWhiteSpace(senderEmail))
                throw new InvalidOperationException(
                    "HostingerMail:SenderEmail is missing.");

            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException(
                    "Recipient email address is required.",
                    nameof(to));

            if (!MailboxAddress.TryParse(to, out var recipient))
                throw new ArgumentException(
                    "Recipient email address is invalid.",
                    nameof(to));

            // Hostinger sends from the authorized mailbox.
            // Confirm that the configured address matches it.
            var email = new
            {
                to = new[] { recipient.Address },
                displayName = string.IsNullOrWhiteSpace(senderName)
                    ? "Lily Store"
                    : senderName,
                subject = subject ?? string.Empty,
                html = body ?? string.Empty
            };

            var requestUrl =
                "https://api.mail.hostinger.com/api/v1/mailboxes/" +
                Uri.EscapeDataString(mailboxResourceId) +
                "/send";

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                requestUrl);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiToken);

            request.Content = JsonContent.Create(email);

            try
            {
                _logger.LogInformation(
                    "Sending email to {Recipient} through Hostinger Mail API.",
                    recipient.Address);

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody =
                        await response.Content.ReadAsStringAsync();

                    _logger.LogError(
                        "Hostinger Mail API failed. Status: {StatusCode}. " +
                        "Response: {Response}",
                        (int)response.StatusCode,
                        responseBody);

                    throw new HttpRequestException(
                        $"Hostinger Mail API returned HTTP " +
                        $"{(int)response.StatusCode} " +
                        $"({response.StatusCode}).");
                }

                _logger.LogInformation(
                    "Email sent successfully to {Recipient}.",
                    recipient.Address);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Recipient} through Hostinger.",
                    recipient.Address);

                throw;
            }
        }
    }
}
