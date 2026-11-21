using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Services.Login;

public sealed class QueuedLoginMailer : ILoginMailer
{
    public const int CodeCapacity = 1000;
    public const int NoticeCapacity = 1000;

    private readonly Channel<QueuedLoginMail> _codes = Channel.CreateBounded<QueuedLoginMail>(new BoundedChannelOptions(CodeCapacity) { SingleReader = true });
    private readonly Channel<QueuedLoginMail> _notices = Channel.CreateBounded<QueuedLoginMail>(new BoundedChannelOptions(NoticeCapacity) { SingleReader = true });
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ILogger<QueuedLoginMailer> _logger;

    public QueuedLoginMailer(ILogger<QueuedLoginMailer> logger) => _logger = logger;

    public ChannelReader<QueuedLoginMail> Codes => _codes.Reader;
    public ChannelReader<QueuedLoginMail> Notices => _notices.Reader;
    public SemaphoreSlim Signal => _signal;

    public Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model)
    {
        var channel = templateKey == LoginMailTemplates.Code ? _codes : _notices;
        var queued = channel.Writer.TryWrite(new QueuedLoginMail(to, templateKey, model));
        if (queued) _signal.Release();
        else _logger.LogError("login_mail_failed Template={Template} Reason=queue_full", templateKey);
        return Task.FromResult(queued);
    }
}
