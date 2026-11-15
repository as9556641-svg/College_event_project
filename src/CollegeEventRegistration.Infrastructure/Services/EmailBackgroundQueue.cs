using System.Threading.Channels;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Infrastructure.Services;

public interface IEmailQueue
{
    ValueTask QueueBackgroundEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
    ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken);
}

public class EmailBackgroundQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel;

    public EmailBackgroundQueue()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<EmailMessage>(options);
    }

    public async ValueTask QueueBackgroundEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
