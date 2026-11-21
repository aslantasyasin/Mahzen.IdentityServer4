using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Services.Login;

public sealed class QueuedLoginMailer : ILoginMailer
{
    public const int Capacity = 1000;

    private readonly Channel<QueuedLoginMail> _channel = Channel.CreateBounded<QueuedLoginMail>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    private readonly ILogger<QueuedLoginMailer> _logger;

    public QueuedLoginMailer(ILogger<QueuedLoginMailer> logger) => _logger = logger;

    public ChannelReader<QueuedLoginMail> Reader => _channel.Reader;

    public Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model)
    {
        var queued = _channel.Writer.TryWrite(new QueuedLoginMail(to, templateKey, model));
        if (!queued) _logger.LogError("login_mail_failed Template={Template} Reason=queue_full", templateKey);
        return Task.FromResult(queued);
    }
}
