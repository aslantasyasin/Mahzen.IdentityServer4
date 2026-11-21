using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityServer.Services.Login;

public sealed class LoginProtectionService
{
    public const string AccountUntrustedScope = "account-untrusted";

    private readonly ILoginThrottleStore _store;
    private readonly LoginProtectionOptions _options;
    private readonly ILogger<LoginProtectionService> _logger;
    private readonly ISystemClock _clock;

    public LoginProtectionService(
        ILoginThrottleStore store,
        IOptions<LoginProtectionOptions> options,
        ILogger<LoginProtectionService> logger,
        ISystemClock clock)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task RecordFailedPasswordAsync(string userId, string source, bool whileLockedOut = false)
    {
        if (_options.Mode == LoginProtectionMode.Off) return;
        var now = _clock.UtcNow;
        try
        {
            var sourceFailures = await _store.IncrementAsync(userId, SourceScope(source), WindowStart(now, _options.SourceWindow));
            var accountFailures = await _store.IncrementAsync(userId, AccountUntrustedScope, WindowStart(now, _options.AccountWindow));

            _logger.LogInformation(
                "login_failed_password UserId={UserId} Source={Source} Outcome={Outcome} SourceFailures={SourceFailures} AccountFailures={AccountFailures} Mode={Mode}",
                userId, source, whileLockedOut ? "locked" : "wrong_password", sourceFailures, accountFailures, _options.Mode);

            if (accountFailures == _options.AccountFailureLimit)
                _logger.LogWarning(
                    "login_account_threshold_reached UserId={UserId} AccountFailures={AccountFailures} Window={Window} Mode={Mode}",
                    userId, accountFailures, _options.AccountWindow, _options.Mode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "login_protection_store_failed Step=record UserId={UserId}", userId);
        }
    }

    public async Task EvaluateSuccessfulPasswordAsync(string userId, string source, bool isAdmin)
    {
        if (_options.Mode == LoginProtectionMode.Off) return;
        var now = _clock.UtcNow;
        try
        {
            var sourceFailures = await _store.CountAsync(userId, SourceScope(source), WindowStart(now, _options.SourceWindow));
            var accountFailures = await _store.CountAsync(userId, AccountUntrustedScope, WindowStart(now, _options.AccountWindow));

            var reason = sourceFailures >= _options.SourceFailureLimit ? "source"
                : accountFailures >= _options.AccountFailureLimit ? "account"
                : isAdmin ? "admin"
                : null;

            if (reason is not null)
                _logger.LogInformation(
                    "login_step_up_required UserId={UserId} Source={Source} Reason={Reason} SourceFailures={SourceFailures} AccountFailures={AccountFailures} Mode={Mode}",
                    userId, source, reason, sourceFailures, accountFailures, _options.Mode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "login_protection_store_failed Step=evaluate UserId={UserId}", userId);
        }
    }

    public static string SourceScope(string source) => "source:" + source;

    public static DateTimeOffset WindowStart(DateTimeOffset now, TimeSpan window) =>
        new(now.UtcTicks - now.UtcTicks % window.Ticks, TimeSpan.Zero);
}
