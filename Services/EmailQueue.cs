using System.Threading.Channels;

namespace PharmacyAPI.Services
{
    public sealed record EmailMessage(
        string To,
        string Subject,
        string HtmlBody);

    public interface IEmailQueue
    {
        // Returns false when the queue is full.
        bool TryEnqueue(EmailMessage message);
    }

    // =========================================================
    // BACKGROUND EMAIL QUEUE
    // =========================================================
    //
    // Requests only enqueue a message. This hosted service sends
    // them one by one, outside the request, so a slow or failing
    // mail API never delays or fails the caller.
    //
    // On shutdown the queue is closed and the remaining messages
    // are sent within the host shutdown timeout.
    //
    // =========================================================

    public sealed class EmailQueue : BackgroundService, IEmailQueue
    {
        private const int Capacity = 500;

        private readonly Channel<EmailMessage> _channel =
            Channel.CreateBounded<EmailMessage>(
                new BoundedChannelOptions(Capacity)
                {
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait
                });

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailQueue> _logger;

        public EmailQueue(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailQueue> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public bool TryEnqueue(EmailMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            return _channel.Writer.TryWrite(message);
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            // Not bound to stoppingToken: keep reading until the
            // writer is completed in StopAsync, so queued emails
            // are not dropped on shutdown.
            await foreach (var message in
                           _channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var emailService =
                        scope.ServiceProvider
                            .GetRequiredService<IEmailService>();

                    await emailService.SendEmailAsync(
                        message.To,
                        message.Subject,
                        message.HtmlBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send queued email \"{Subject}\".",
                        message.Subject);
                }
            }
        }

        public override Task StopAsync(
            CancellationToken cancellationToken)
        {
            _channel.Writer.TryComplete();

            return base.StopAsync(cancellationToken);
        }
    }
}
