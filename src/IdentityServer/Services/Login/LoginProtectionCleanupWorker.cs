using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityServer.Services.Login;

public sealed class LoginProtectionCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly LoginProtectionOptions _options;
    private readonly ILogger<LoginProtectionCleanupWorker> _logger;

    public LoginProtectionCleanupWorker(IServiceScopeFactory scopes, IOptions<LoginProtectionOptions> options, ILogger<LoginProtectionCleanupWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Mode == LoginProtectionMode.Off) return;
        using var timer = new PeriodicTimer(_options.CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<LoginProtectionService>().CleanupAsync();
                _logger.LogInformation("login_protection_cleanup Deleted={Deleted}", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "login_protection_cleanup_failed counters and expired devices were not removed this round");
            }
        }
    }
}
