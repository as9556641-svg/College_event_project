using CollegeEventRegistration.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Reliable background worker for processing non-blocking asynchronous email delivery.
/// </summary>
public class QueuedEmailBackgroundService : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueuedEmailBackgroundService> _logger;

    public QueuedEmailBackgroundService(
        IEmailQueue queue,
        IServiceProvider serviceProvider,
        ILogger<QueuedEmailBackgroundService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QueuedEmailBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var email = await _queue.DequeueAsync(stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var result = await emailService.SendEmailAsync(
                    email.ToEmail,
                    email.Subject,
                    email.BodyHtml,
                    stoppingToken);

                _logger.LogInformation(
                    "Background email dispatched to {Recipient}. Status: {Status}",
                    email.ToEmail, result.Status);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background email dispatch worker: {Message}", ex.Message);
            }
        }

        _logger.LogInformation("QueuedEmailBackgroundService stopped.");
    }
}
