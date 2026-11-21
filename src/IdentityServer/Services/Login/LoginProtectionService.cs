using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityServer.Services.Login;

public sealed class LoginProtectionService
{
    public const string AccountUntrustedScope = "account-untrusted";
    public const string ChallengeSendsScope = "challenge-sends";
    public const string StepUpFailuresScope = "step-up-failures";
    public const string AttackNoticeKind = "attack";
    public const string UnderAttackKind = "under-attack";
    public const int CleanupBatchSize = 5000;

    private static readonly TimeSpan ChallengeSendWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan TurkeyUtcOffset = TimeSpan.FromHours(3);

    private readonly ILoginProtectionStore _store;
    private readonly ILoginMailer _mailer;
    private readonly LoginFallbackLimiter _fallback;
    private readonly LoginProtectionOptions _options;
    private readonly ILogger<LoginProtectionService> _logger;
    private readonly ISystemClock _clock;

    public LoginProtectionService(
        ILoginProtectionStore store,
        ILoginMailer mailer,
        LoginFallbackLimiter fallback,
        IOptions<LoginProtectionOptions> options,
        ILogger<LoginProtectionService> logger,
        ISystemClock clock)
    {
        _store = store;
        _mailer = mailer;
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public bool Enforcing => _options.Mode == LoginProtectionMode.Enforce;

    public async Task<long?> FindTrustedDeviceAsync(string userId, string deviceToken)
    {
        if (_options.Mode == LoginProtectionMode.Off || string.IsNullOrWhiteSpace(deviceToken)) return null;
        try
        {
            return await _store.FindActiveDeviceAsync(userId, LoginSecrets.HashDeviceToken(deviceToken), _clock.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login_protection_store_failed Step=device UserId={UserId}", userId);
            return null;
        }
    }

    public async Task<LoginGate> GetGateAsync(string userId, string source, long? deviceId, bool isAdmin)
    {
        if (_options.Mode == LoginProtectionMode.Off || deviceId is not null) return LoginGate.Open;
        var now = _clock.UtcNow;
        try
        {
            var sourceFailures = await RecentFailuresAsync(userId, SourceScope(source), _options.SourceWindow, now);
            var accountFailures = await RecentFailuresAsync(userId, AccountUntrustedScope, _options.AccountWindow, now);
            var underAttackSince = await _store.GetFlagAsync(userId, UnderAttackKind);
            var reason = sourceFailures >= _options.SourceFailureLimit ? "source"
                : accountFailures >= _options.AccountFailureLimit || underAttackSince > now - _options.UnderAttackDuration ? "account"
                : isAdmin ? "admin"
                : null;
            if (reason is null) return LoginGate.Open;

            _logger.LogInformation(
                "login_step_up_required UserId={UserId} Source={Source} Reason={Reason} SourceFailures={SourceFailures} AccountFailures={AccountFailures} Mode={Mode}",
                userId, source, reason, sourceFailures, accountFailures, _options.Mode);
            return Enforcing ? new LoginGate(LoginGateKind.StepUp, reason) : LoginGate.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login_protection_store_failed Step=gate UserId={UserId} Mode={Mode} falling back to in-memory limits", userId, _options.Mode);
            if (!Enforcing) return LoginGate.Open;
            if (isAdmin) return new LoginGate(LoginGateKind.Unavailable, "admin");
            return _fallback.IsBlocked(userId, source, _options.SourceFailureLimit) ? new LoginGate(LoginGateKind.Blocked, "fallback") : LoginGate.Open;
        }
    }

    public async Task RecordFailedPasswordAsync(string userId, string email, string source, long? deviceId, bool whileLockedOut = false)
    {
        if (_options.Mode == LoginProtectionMode.Off) return;
        var now = _clock.UtcNow;
        var outcome = whileLockedOut ? "locked" : "wrong_password";
        try
        {
            if (deviceId is { } id)
            {
                var deviceFailures = await _store.IncrementAsync(userId, DeviceScope(id), WindowStart(now, _options.DeviceWindow));
                _logger.LogInformation(
                    "login_failed_password UserId={UserId} Source={Source} Outcome={Outcome} Device={DeviceId} DeviceFailures={DeviceFailures} Mode={Mode}",
                    userId, source, outcome, id, deviceFailures, _options.Mode);
                if (deviceFailures >= _options.DeviceFailureLimit)
                {
                    await _store.RevokeDeviceAsync(id, now, "failures");
                    _logger.LogWarning("login_device_revoked UserId={UserId} Device={DeviceId} Reason=failures DeviceFailures={DeviceFailures}",
                        userId, id, deviceFailures);
                }
                return;
            }

            await _store.IncrementAsync(userId, SourceScope(source), WindowStart(now, _options.SourceWindow));
            await _store.IncrementAsync(userId, AccountUntrustedScope, WindowStart(now, _options.AccountWindow));
            var sourceFailures = await RecentFailuresAsync(userId, SourceScope(source), _options.SourceWindow, now);
            var accountFailures = await RecentFailuresAsync(userId, AccountUntrustedScope, _options.AccountWindow, now);

            _logger.LogInformation(
                "login_failed_password UserId={UserId} Source={Source} Outcome={Outcome} SourceFailures={SourceFailures} AccountFailures={AccountFailures} Mode={Mode}",
                userId, source, outcome, sourceFailures, accountFailures, _options.Mode);

            if (accountFailures >= _options.AccountFailureLimit)
            {
                await _store.MarkFlagAsync(userId, UnderAttackKind, now);
                if (await _store.TryClaimNoticeAsync(userId, AttackNoticeKind, now, now - _options.AttackNoticeInterval))
                {
                    _logger.LogWarning(
                        "login_account_threshold_reached UserId={UserId} AccountFailures={AccountFailures} Window={Window} Mode={Mode}",
                        userId, accountFailures, _options.AccountWindow, _options.Mode);
                    if (Enforcing && !string.IsNullOrWhiteSpace(email))
                        await _mailer.SendAsync(email, LoginMailTemplates.AttackNotice,
                            new Dictionary<string, string> { ["At"] = TurkeyTime(now), ["Failures"] = accountFailures.ToString(CultureInfo.InvariantCulture) });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login_protection_store_failed Step=record UserId={UserId} counted in memory instead", userId);
            _fallback.RecordFailure(userId, source, _options.SourceWindow);
        }
    }

    public async Task<LoginOutcome> CompleteAsync(LoginAttempt attempt, LoginGate gate, bool credentialsOk)
    {
        if (gate.Kind == LoginGateKind.Blocked) return LoginOutcome.Rejected();
        if (gate.Kind == LoginGateKind.Unavailable) return LoginOutcome.TemporarilyUnavailable();
        if (_options.Mode == LoginProtectionMode.Off) return credentialsOk ? LoginOutcome.Allowed() : LoginOutcome.Rejected();

        var now = _clock.UtcNow;
        try
        {
            if (Enforcing && !string.IsNullOrEmpty(attempt.StepUpId))
                return await VerifyStepUpAsync(attempt, credentialsOk, now);

            if (gate.Kind == LoginGateKind.StepUp)
                return await StepUpAsync(attempt, credentialsOk, now);

            if (!credentialsOk) return LoginOutcome.Rejected();

            if (attempt.DeviceId is { } deviceId)
            {
                await _store.TouchDeviceAsync(deviceId, now, now + _options.DeviceLifetime);
                return LoginOutcome.Allowed();
            }

            return LoginOutcome.Allowed(await IssueDeviceAsync(attempt, now));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login_protection_store_failed Step=complete UserId={UserId} Mode={Mode}", attempt.UserId, _options.Mode);
            if (!credentialsOk) return LoginOutcome.Rejected();
            return Enforcing && attempt.IsAdmin && attempt.DeviceId is null ? LoginOutcome.TemporarilyUnavailable() : LoginOutcome.Allowed();
        }
    }

    public async Task ResetUserAsync(string userId, string reason)
    {
        try
        {
            await _store.ResetUserAsync(userId, _clock.UtcNow, reason);
            _logger.LogInformation("login_protection_reset UserId={UserId} Reason={Reason}", userId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login_protection_store_failed Step=reset UserId={UserId} Reason={Reason} devices and counters were not cleared", userId, reason);
        }
    }

    public Task<int> CleanupAsync()
    {
        var now = _clock.UtcNow;
        var longestWindow = new[] { _options.SourceWindow, _options.AccountWindow, _options.DeviceWindow, _options.StepUpFailureWindow, ChallengeSendWindow }.Max();
        return _store.CleanupAsync(now, WindowStart(now, longestWindow) - longestWindow, CleanupBatchSize);
    }

    private async Task<int> RecentFailuresAsync(string userId, string scope, TimeSpan window, DateTimeOffset now)
    {
        var current = WindowStart(now, window);
        return await _store.CountAsync(userId, scope, current) + await _store.CountAsync(userId, scope, current - window);
    }

    private async Task<LoginOutcome> StepUpAsync(LoginAttempt attempt, bool credentialsOk, DateTimeOffset now)
    {
        var challenge = await _store.FindOpenChallengeAsync(attempt.UserId, now);
        var challengeId = challenge?.Id ?? Guid.NewGuid();
        if (challenge is null)
            await _store.CreateChallengeAsync(challengeId, attempt.UserId, LoginSecrets.HashCode(challengeId, LoginSecrets.NewCode()),
                _options.ChallengeMaxAttempts, now, now + _options.ChallengeLifetime);

        if (credentialsOk) await SendCodeAsync(attempt, challengeId, challenge?.LastSentAt, now);
        return LoginOutcome.StepUpRequired(challengeId);
    }

    private async Task SendCodeAsync(LoginAttempt attempt, Guid challengeId, DateTimeOffset? lastSentAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(attempt.Email))
        {
            _logger.LogError("login_step_up_unavailable UserId={UserId} Reason=no_email", attempt.UserId);
            return;
        }
        if (lastSentAt > now - _options.ChallengeResendCooldown) return;
        if (await _store.IncrementAsync(attempt.UserId, ChallengeSendsScope, WindowStart(now, ChallengeSendWindow)) > _options.ChallengeSendsPerHour)
        {
            _logger.LogWarning("login_step_up_send_budget_exhausted UserId={UserId} Challenge={ChallengeId}", attempt.UserId, challengeId);
            return;
        }

        var code = LoginSecrets.NewCode();
        if (!await _store.TryRotateChallengeCodeAsync(challengeId, LoginSecrets.HashCode(challengeId, code), now, now - _options.ChallengeResendCooldown))
            return;
        var queued = await _mailer.SendAsync(attempt.Email, LoginMailTemplates.Code, new Dictionary<string, string> { ["Code"] = code });
        _logger.LogInformation("login_step_up_code_sent UserId={UserId} Challenge={ChallengeId} Queued={Queued}", attempt.UserId, challengeId, queued);
    }

    private async Task<LoginOutcome> VerifyStepUpAsync(LoginAttempt attempt, bool credentialsOk, DateTimeOffset now)
    {
        if (!credentialsOk) return StepUpFailed(attempt, "credentials");
        if (!Guid.TryParse(attempt.StepUpId, out var challengeId) || string.IsNullOrEmpty(attempt.StepUpCode))
            return StepUpFailed(attempt, "malformed");

        if (await RecentFailuresAsync(attempt.UserId, StepUpFailuresScope, _options.StepUpFailureWindow, now) >= _options.StepUpFailureLimit)
            return StepUpFailed(attempt, "too_many_wrong_codes");

        var challenge = await _store.GetChallengeAsync(challengeId);
        if (challenge is null || challenge.UserId != attempt.UserId)
            return StepUpFailed(attempt, "unknown_challenge");

        if (!await _store.TryConsumeChallengeAttemptAsync(challengeId, now))
            return StepUpFailed(attempt, "no_attempts_left");

        if (!LoginSecrets.HashesMatch(challenge.CodeHash, LoginSecrets.HashCode(challengeId, attempt.StepUpCode.Trim())))
        {
            await _store.IncrementAsync(attempt.UserId, StepUpFailuresScope, WindowStart(now, _options.StepUpFailureWindow));
            return StepUpFailed(attempt, "wrong_code");
        }

        if (!await _store.TryCompleteChallengeAsync(challengeId, now))
            return StepUpFailed(attempt, "already_used");

        _logger.LogInformation("login_step_up_passed UserId={UserId} Source={Source}", attempt.UserId, attempt.Source);
        return LoginOutcome.Allowed(attempt.DeviceId is null ? await IssueDeviceAsync(attempt, now) : null);
    }

    private async Task<string> IssueDeviceAsync(LoginAttempt attempt, DateTimeOffset now)
    {
        var token = LoginSecrets.NewDeviceToken();
        await _store.AddDeviceAsync(attempt.UserId, LoginSecrets.HashDeviceToken(token), now, now + _options.DeviceLifetime);
        await _store.RevokeExcessDevicesAsync(attempt.UserId, _options.MaxDevicesPerUser, now);
        _logger.LogInformation("login_device_issued UserId={UserId} Source={Source}", attempt.UserId, attempt.Source);

        if (Enforcing && _options.NotifyNewDevice && !string.IsNullOrWhiteSpace(attempt.Email))
            await _mailer.SendAsync(attempt.Email, LoginMailTemplates.NewDevice, new Dictionary<string, string> { ["At"] = TurkeyTime(now) });
        return token;
    }

    private LoginOutcome StepUpFailed(LoginAttempt attempt, string reason)
    {
        _logger.LogInformation("login_step_up_failed UserId={UserId} Source={Source} Reason={Reason}", attempt.UserId, attempt.Source, reason);
        return LoginOutcome.StepUpFailed();
    }

    private static string TurkeyTime(DateTimeOffset now) =>
        now.ToOffset(TurkeyUtcOffset).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string SourceScope(string source) => "source:" + source;

    public static string DeviceScope(long deviceId) => "device:" + deviceId.ToString(CultureInfo.InvariantCulture);

    public static DateTimeOffset WindowStart(DateTimeOffset now, TimeSpan window) =>
        new(now.UtcTicks - now.UtcTicks % window.Ticks, TimeSpan.Zero);
}
