using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Services.Login;

public sealed class LoginMailWorker : BackgroundService
{
    private readonly QueuedLoginMailer _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LoginMailWorker> _logger;

    public LoginMailWorker(QueuedLoginMailer queue, IServiceScopeFactory scopes, ILogger<LoginMailWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await _queue.Signal.WaitAsync(stoppingToken);
                if (!_queue.Codes.TryRead(out var mail) && !_queue.Notices.TryRead(out mail)) continue;
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificationLoginClient>().SendAsync(mail.To, mail.TemplateKey, mail.Model);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            var pending = _queue.Codes.Count + _queue.Notices.Count;
            if (pending > 0) _logger.LogWarning("login_mail_stopped_with_pending Count={Count}", pending);
        }
    }
}
