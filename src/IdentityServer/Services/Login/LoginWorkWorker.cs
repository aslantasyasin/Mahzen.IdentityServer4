using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Services.Login;

public sealed class LoginWorkWorker : BackgroundService
{
    private readonly LoginWorkQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LoginWorkWorker> _logger;

    public LoginWorkWorker(LoginWorkQueue queue, IServiceScopeFactory scopes, ILogger<LoginWorkWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (name, job) in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await job(scope.ServiceProvider.GetRequiredService<LoginProtectionService>());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "login_work_failed Job={Job}", name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (_queue.Reader.Count > 0)
                _logger.LogWarning("login_work_stopped_with_pending Count={Count}", _queue.Reader.Count);
        }
    }
}
