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
            await foreach (var mail in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificationLoginClient>().SendAsync(mail.To, mail.TemplateKey, mail.Model);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (_queue.Reader.Count > 0)
                _logger.LogWarning("login_mail_stopped_with_pending Count={Count}", _queue.Reader.Count);
        }
    }
}
