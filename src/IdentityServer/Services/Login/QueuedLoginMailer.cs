using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Services.Login;

public sealed class QueuedLoginMailer : ILoginMailer
{
    public const int CodeCapacity = 1000;
    public const int NoticeCapacity = 200;
    public const int NoticesPerHour = 200;

    private readonly Channel<QueuedLoginMail> _codes = Channel.CreateBounded<QueuedLoginMail>(new BoundedChannelOptions(CodeCapacity) { SingleReader = true });

    private readonly Channel<QueuedLoginMail> _notices = Channel.CreateBounded<QueuedLoginMail>(new BoundedChannelOptions(NoticeCapacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    private readonly ILogger<QueuedLoginMailer> _logger;
    private readonly ISystemClock _clock;
    private readonly object _gate = new();
    private DateTimeOffset _noticeHour;
    private int _noticesThisHour;

    public QueuedLoginMailer(ILogger<QueuedLoginMailer> logger, ISystemClock clock)
    {
        _logger = logger;
        _clock = clock;
    }

    public ChannelReader<QueuedLoginMail> Codes => _codes.Reader;
    public ChannelReader<QueuedLoginMail> Notices => _notices.Reader;

    public Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model)
    {
        var mail = new QueuedLoginMail(to, templateKey, model);
        if (templateKey == LoginMailTemplates.Code)
        {
            var queued = _codes.Writer.TryWrite(mail);
            if (!queued) _logger.LogError("login_mail_failed Template={Template} Reason=queue_full", templateKey);
            return Task.FromResult(queued);
        }

        lock (_gate)
        {
            var hour = LoginProtectionService.WindowStart(_clock.UtcNow, TimeSpan.FromHours(1));
            if (hour != _noticeHour) { _noticeHour = hour; _noticesThisHour = 0; }
            if (++_noticesThisHour > NoticesPerHour)
            {
                _logger.LogWarning("login_mail_failed Template={Template} Reason=notice_cap", templateKey);
                return Task.FromResult(false);
            }
        }
        return Task.FromResult(_notices.Writer.TryWrite(mail));
    }
}
