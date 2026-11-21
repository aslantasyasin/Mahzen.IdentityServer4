using System.Net;
using IdentityServer.Services.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityServer.Tests;

public class LoginSourceTests
{
    private const string Key = "internal-key";

    private static HttpContext Request(string? internalKey, string? clientId, string ip = "10.0.0.7")
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (internalKey is not null) http.Request.Headers["X-Internal-Key"] = internalKey;
        if (clientId is not null) http.Request.Headers["X-ClientId"] = clientId;
        return http;
    }

    [Fact]
    public void Client_id_is_trusted_only_with_the_internal_key() =>
        Assert.Equal("client:ip_95_70_1_2", LoginSource.Resolve(Request(Key, "ip_95_70_1_2"), Key));

    [Theory]
    [InlineData("wrong-key", "ip_1_2_3_4")]
    [InlineData(null, "ip_1_2_3_4")]
    [InlineData(Key, null)]
    [InlineData(Key, "")]
    [InlineData(Key, "bad id with spaces")]
    public void Otherwise_the_connection_ip_is_used(string? internalKey, string? clientId) =>
        Assert.Equal("ip:10.0.0.7", LoginSource.Resolve(Request(internalKey, clientId), Key));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Unconfigured_key_never_trusts_the_header(string? configuredKey) =>
        Assert.Equal("ip:10.0.0.7", LoginSource.Resolve(Request("", "ip_1_2_3_4"), configuredKey!));

    [Fact]
    public void Client_id_longer_than_the_limit_is_ignored() =>
        Assert.Equal("ip:10.0.0.7", LoginSource.Resolve(Request(Key, new string('a', 129)), Key));

    [Fact]
    public void Missing_context_falls_back_to_unknown() =>
        Assert.Equal("ip:unknown", LoginSource.Resolve(null!, Key));
}

public class LoginProtectionServiceTests
{
    private const string User = "user-1";
    private const string Email = "ali@ornek.com";
    private const string Source = "client:ip_1_1_1_1";
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 10, 14, 59, TimeSpan.Zero);

    internal sealed class MemoryStore : ILoginProtectionStore
    {
        private readonly object _gate = new();
        private long _nextDevice;
        public Dictionary<(string User, string Scope, DateTimeOffset Window), int> Counts { get; } = new();
        public List<(long Id, string User, string Hash, DateTimeOffset Expires, DateTimeOffset? Revoked, string Reason)> Devices { get; } = new();
        public Dictionary<Guid, (string User, string Hash, int Attempts, int Max, DateTimeOffset Created, DateTimeOffset Expires, DateTimeOffset LastSent, DateTimeOffset? Used)> Challenges { get; } = new();
        public Dictionary<(string User, string Kind), DateTimeOffset> Notices { get; } = new();
        public bool Broken { get; set; }

        private void Check() { if (Broken) throw new InvalidOperationException("db down"); }

        public Task<int> IncrementAsync(string userId, string scope, DateTimeOffset windowStart)
        {
            Check();
            lock (_gate) { var k = (userId, scope, windowStart); Counts[k] = Counts.GetValueOrDefault(k) + 1; return Task.FromResult(Counts[k]); }
        }

        public Task<int> CountAsync(string userId, string scope, DateTimeOffset windowStart)
        {
            Check();
            lock (_gate) return Task.FromResult(Counts.GetValueOrDefault((userId, scope, windowStart)));
        }

        public Task<long?> FindActiveDeviceAsync(string userId, string tokenHash, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                var d = Devices.FirstOrDefault(x => x.Hash == tokenHash && x.User == userId && x.Revoked is null && x.Expires > now);
                return Task.FromResult(d.Id == 0 ? (long?)null : d.Id);
            }
        }

        public Task AddDeviceAsync(string userId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
        {
            Check();
            lock (_gate) Devices.Add((++_nextDevice, userId, tokenHash, expiresAt, null, null));
            return Task.CompletedTask;
        }

        public Task TouchDeviceAsync(long deviceId, DateTimeOffset now, DateTimeOffset expiresAt)
        {
            Check();
            lock (_gate) { var i = Devices.FindIndex(x => x.Id == deviceId); Devices[i] = Devices[i] with { Expires = expiresAt }; }
            return Task.CompletedTask;
        }

        public Task RevokeDeviceAsync(long deviceId, DateTimeOffset now, string reason)
        {
            Check();
            lock (_gate) { var i = Devices.FindIndex(x => x.Id == deviceId); Devices[i] = Devices[i] with { Revoked = now, Reason = reason }; }
            return Task.CompletedTask;
        }

        public Task<LoginChallenge> FindOpenChallengeAsync(string userId, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                var c = Challenges.Where(x => x.Value.User == userId && x.Value.Used is null && x.Value.Expires > now && x.Value.Attempts < x.Value.Max)
                    .OrderByDescending(x => x.Value.Created).Select(x => Map(x.Key)).FirstOrDefault();
                return Task.FromResult(c);
            }
        }

        public Task<LoginChallenge> GetChallengeAsync(Guid id)
        {
            Check();
            lock (_gate) return Task.FromResult(Challenges.ContainsKey(id) ? Map(id) : null);
        }

        private LoginChallenge Map(Guid id)
        {
            var c = Challenges[id];
            return new LoginChallenge(id, c.User, c.Hash, c.Expires, c.LastSent, c.Used is not null);
        }

        public Task CreateChallengeAsync(Guid id, string userId, string codeHash, int maxAttempts, DateTimeOffset now, DateTimeOffset expiresAt)
        {
            Check();
            lock (_gate) Challenges[id] = (userId, codeHash, 0, maxAttempts, now, expiresAt, now, null);
            return Task.CompletedTask;
        }

        public Task<bool> TryRotateChallengeCodeAsync(Guid id, string codeHash, DateTimeOffset now, DateTimeOffset sentBefore)
        {
            Check();
            lock (_gate)
            {
                var c = Challenges[id];
                if (c.Used is not null || c.Expires <= now || c.LastSent > sentBefore) return Task.FromResult(false);
                Challenges[id] = c with { Hash = codeHash, LastSent = now };
                return Task.FromResult(true);
            }
        }

        public Task<bool> TryConsumeChallengeAttemptAsync(Guid id, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                var c = Challenges[id];
                if (c.Used is not null || c.Expires <= now || c.Attempts >= c.Max) return Task.FromResult(false);
                Challenges[id] = c with { Attempts = c.Attempts + 1 };
                return Task.FromResult(true);
            }
        }

        public Task<bool> TryCompleteChallengeAsync(Guid id, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                var c = Challenges[id];
                if (c.Used is not null) return Task.FromResult(false);
                Challenges[id] = c with { Used = now };
                return Task.FromResult(true);
            }
        }

        public Task<bool> TryClaimNoticeAsync(string userId, string kind, DateTimeOffset now, DateTimeOffset sentBefore)
        {
            Check();
            lock (_gate)
            {
                if (Notices.TryGetValue((userId, kind), out var sent) && sent > sentBefore) return Task.FromResult(false);
                Notices[(userId, kind)] = now;
                return Task.FromResult(true);
            }
        }

        public Task ResetUserAsync(string userId, DateTimeOffset now, string reason)
        {
            Check();
            lock (_gate)
            {
                foreach (var k in Counts.Keys.Where(k => k.User == userId).ToList()) Counts.Remove(k);
                for (var i = 0; i < Devices.Count; i++)
                    if (Devices[i].User == userId && Devices[i].Revoked is null) Devices[i] = Devices[i] with { Revoked = now, Reason = reason };
                foreach (var id in Challenges.Keys.ToList())
                    if (Challenges[id].User == userId && Challenges[id].Used is null) Challenges[id] = Challenges[id] with { Used = now };
            }
            return Task.CompletedTask;
        }

        public DateTimeOffset? LastCleanupThrottleBefore { get; private set; }

        public Task<int> CleanupAsync(DateTimeOffset now, DateTimeOffset throttleBefore, int batchSize)
        {
            LastCleanupThrottleBefore = throttleBefore;
            return Task.FromResult(0);
        }
    }

    internal sealed class RecordingMailer : ILoginMailer
    {
        public List<(string To, string Template, IReadOnlyDictionary<string, string> Model)> Sent { get; } = new();
        public Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model)
        {
            lock (Sent) Sent.Add((to, templateKey, model));
            return Task.FromResult(true);
        }
        public string LastCode => Sent.Last(m => m.Template == LoginMailTemplates.Code).Model["Code"];
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = Start;
    }

    private sealed class ListLogger : ILogger<LoginProtectionService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries) Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private readonly MemoryStore _store = new();
    private readonly RecordingMailer _mail = new();
    private readonly FixedClock _clock = new();
    private readonly ListLogger _log = new();

    private LoginProtectionService Service(LoginProtectionMode mode = LoginProtectionMode.Enforce) =>
        new(_store, _mail, Options.Create(new LoginProtectionOptions { Mode = mode }), _log, _clock);

    private static LoginAttempt Attempt(string source = Source, long? device = null, bool admin = false, string? stepUpId = null, string? code = null, string user = User, string email = Email) =>
        new(user, email, source, device, admin, stepUpId!, code!);

    private async Task Fail(LoginProtectionService service, int times, string source = Source, long? device = null)
    {
        for (var i = 0; i < times; i++) await service.RecordFailedPasswordAsync(User, Email, source, device);
    }

    private IEnumerable<string> Messages(string prefix) => _log.Entries.Select(e => e.Message).Where(m => m.StartsWith(prefix));

    [Fact]
    public async Task Off_mode_touches_nothing()
    {
        var service = Service(LoginProtectionMode.Off);
        await service.RecordFailedPasswordAsync(User, Email, Source, null);
        var outcome = await service.CompleteAsync(Attempt(admin: true));

        Assert.Equal(LoginOutcome.Allowed(), outcome);
        Assert.Empty(_store.Counts);
        Assert.Empty(_store.Devices);
        Assert.Empty(_mail.Sent);
        Assert.Null(await service.FindTrustedDeviceAsync(User, "anything"));
    }

    [Fact]
    public async Task A_failure_counts_for_the_source_and_the_account_in_their_own_windows()
    {
        await Fail(Service(), 1);

        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
        Assert.Equal(1, _store.Counts[(User, LoginProtectionService.AccountUntrustedScope, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
    }

    [Theory]
    [InlineData(false, "Outcome=wrong_password")]
    [InlineData(true, "Outcome=locked")]
    public async Task Attempts_while_locked_out_are_counted_and_labelled(bool locked, string label)
    {
        await Service(LoginProtectionMode.Shadow).RecordFailedPasswordAsync(User, Email, Source, null, whileLockedOut: locked);

        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
        Assert.Contains(Messages("login_failed_password"), m => m.Contains(label));
    }

    [Fact]
    public async Task Source_window_rolls_over_at_its_boundary()
    {
        var service = Service();
        await Fail(service, 1);
        _clock.UtcNow = Start.AddSeconds(1);
        await Fail(service, 1);

        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 15, 0, TimeSpan.Zero))]);
        Assert.Equal(2, _store.Counts[(User, LoginProtectionService.AccountUntrustedScope, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
    }

    [Fact]
    public async Task A_clean_login_is_allowed_and_gets_a_hashed_device_token_and_a_new_device_notice()
    {
        var outcome = await Service().CompleteAsync(Attempt());

        Assert.Equal(LoginOutcomeKind.Allowed, outcome.Kind);
        Assert.False(string.IsNullOrEmpty(outcome.DeviceToken));
        var device = Assert.Single(_store.Devices);
        Assert.Equal(LoginSecrets.HashDeviceToken(outcome.DeviceToken!), device.Hash);
        Assert.NotEqual(outcome.DeviceToken, device.Hash);
        Assert.Equal(Start.AddDays(90), device.Expires);
        Assert.Equal(LoginMailTemplates.NewDevice, Assert.Single(_mail.Sent).Template);
    }

    [Theory]
    [InlineData(4, LoginOutcomeKind.Allowed)]
    [InlineData(5, LoginOutcomeKind.StepUpRequired)]
    public async Task Source_threshold_asks_for_a_code_from_the_fifth_failure(int failures, LoginOutcomeKind expected)
    {
        var service = Service();
        await Fail(service, failures);

        Assert.Equal(expected, (await service.CompleteAsync(Attempt())).Kind);
    }

    [Fact]
    public async Task Shadow_only_logs_the_step_up_and_lets_the_user_in()
    {
        var service = Service(LoginProtectionMode.Shadow);
        await Fail(service, 5);

        var outcome = await service.CompleteAsync(Attempt());

        Assert.Equal(LoginOutcomeKind.Allowed, outcome.Kind);
        Assert.Contains(Messages("login_step_up_required"), m => m.Contains("Reason=source") && m.Contains("Mode=Shadow"));
        Assert.DoesNotContain(_mail.Sent, m => m.Template == LoginMailTemplates.Code);
    }

    [Fact]
    public async Task Failures_split_across_a_window_boundary_still_count()
    {
        var service = Service();
        await Fail(service, 3);
        _clock.UtcNow = Start.AddSeconds(1);
        await Fail(service, 2);

        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await service.CompleteAsync(Attempt())).Kind);
    }

    [Fact]
    public async Task Failures_older_than_two_windows_are_forgotten()
    {
        var service = Service();
        await Fail(service, 5);
        _clock.UtcNow = Start.AddMinutes(16);

        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt())).Kind);
    }

    [Fact]
    public async Task Shadow_never_sends_mail_to_users()
    {
        var service = Service(LoginProtectionMode.Shadow);
        for (var i = 0; i < 25; i++) await service.RecordFailedPasswordAsync(User, Email, $"client:ip_{i}", null);
        await service.CompleteAsync(Attempt());
        await service.CompleteAsync(Attempt(admin: true, user: "admin-1"));

        Assert.Empty(_mail.Sent);
        Assert.NotEmpty(_store.Devices);
    }

    [Fact]
    public async Task Failures_from_another_source_do_not_affect_this_source()
    {
        var service = Service();
        await Fail(service, 5, source: "client:ip_6_6_6_6");

        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt())).Kind);
    }

    [Fact]
    public async Task Account_threshold_asks_every_untrusted_source_for_a_code_and_notifies_once_a_day()
    {
        var service = Service();
        for (var i = 0; i < 25; i++) await service.RecordFailedPasswordAsync(User, Email, $"client:ip_{i}", null);
        _clock.UtcNow = Start.AddHours(1);
        for (var i = 0; i < 20; i++) await service.RecordFailedPasswordAsync(User, Email, $"client:ip_{i}", null);

        Assert.Single(_mail.Sent, m => m.Template == LoginMailTemplates.AttackNotice);
        Assert.Equal(2, Messages("login_account_threshold_reached").Count());
        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await service.CompleteAsync(Attempt(source: "client:ip_fresh"))).Kind);
    }

    [Fact]
    public async Task Admin_needs_a_code_on_an_untrusted_device_but_not_on_a_trusted_one()
    {
        var service = Service();
        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await service.CompleteAsync(Attempt(admin: true))).Kind);

        await _store.AddDeviceAsync(User, LoginSecrets.HashDeviceToken("admin-device"), Start, Start.AddDays(1));
        var device = await service.FindTrustedDeviceAsync(User, "admin-device");
        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt(admin: true, device: device))).Kind);
    }

    [Fact]
    public async Task Principle_attacker_cannot_lock_the_owner_out()
    {
        var service = Service();
        var first = await service.CompleteAsync(Attempt());
        var ownerDevice = await service.FindTrustedDeviceAsync(User, first.DeviceToken!);

        await Fail(service, 100);
        for (var i = 0; i < 100; i++) await service.RecordFailedPasswordAsync(User, Email, $"client:ip_{i}", null);

        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt(device: ownerDevice))).Kind);

        var stepUp = await service.CompleteAsync(Attempt());
        Assert.Equal(LoginOutcomeKind.StepUpRequired, stepUp.Kind);
        var verified = await service.CompleteAsync(Attempt(stepUpId: stepUp.ChallengeId.ToString(), code: _mail.LastCode));
        Assert.Equal(LoginOutcomeKind.Allowed, verified.Kind);
        Assert.NotNull(verified.DeviceToken);
    }

    [Fact]
    public async Task Code_attempts_are_spent_before_comparing_so_the_right_code_fails_after_five_wrong_ones()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await service.CompleteAsync(Attempt());
        var code = _mail.LastCode;

        for (var i = 0; i < 5; i++)
            Assert.Equal(LoginOutcomeKind.StepUpFailed, (await service.CompleteAsync(Attempt(stepUpId: stepUp.ChallengeId.ToString(), code: code == "000000" ? "111111" : "000000"))).Kind);

        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await service.CompleteAsync(Attempt(stepUpId: stepUp.ChallengeId.ToString(), code: code))).Kind);
    }

    [Fact]
    public async Task A_challenge_cannot_be_used_by_another_user_or_twice()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await service.CompleteAsync(Attempt());
        var code = _mail.LastCode;

        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await service.CompleteAsync(Attempt(user: "user-2", stepUpId: stepUp.ChallengeId.ToString(), code: code))).Kind);
        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt(stepUpId: stepUp.ChallengeId.ToString(), code: code))).Kind);
        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await service.CompleteAsync(Attempt(stepUpId: stepUp.ChallengeId.ToString(), code: code))).Kind);
    }

    [Fact]
    public async Task Repeated_logins_reuse_the_challenge_and_resend_only_after_the_cooldown()
    {
        var service = Service();
        await Fail(service, 5);
        var first = await service.CompleteAsync(Attempt());
        var firstCode = _mail.LastCode;

        var again = await service.CompleteAsync(Attempt());
        Assert.Equal(first.ChallengeId, again.ChallengeId);
        Assert.Single(_mail.Sent, m => m.Template == LoginMailTemplates.Code);

        _clock.UtcNow = Start.AddSeconds(61);
        var resent = await service.CompleteAsync(Attempt());
        Assert.Equal(first.ChallengeId, resent.ChallengeId);
        Assert.Equal(2, _mail.Sent.Count(m => m.Template == LoginMailTemplates.Code));
        var newCode = _mail.LastCode;

        if (newCode != firstCode)
            Assert.Equal(LoginOutcomeKind.StepUpFailed, (await service.CompleteAsync(Attempt(stepUpId: first.ChallengeId.ToString(), code: firstCode))).Kind);
        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt(stepUpId: first.ChallengeId.ToString(), code: newCode))).Kind);
    }

    [Fact]
    public async Task Code_mails_are_capped_per_hour()
    {
        var service = Service();
        await Fail(service, 5);
        for (var i = 0; i < 8; i++)
        {
            _clock.UtcNow = Start.AddSeconds(61 * i);
            await service.CompleteAsync(Attempt());
        }

        Assert.Equal(5, _mail.Sent.Count(m => m.Template == LoginMailTemplates.Code));
        Assert.NotEmpty(Messages("login_step_up_send_budget_exhausted"));
    }

    [Fact]
    public async Task Ten_wrong_passwords_on_a_trusted_device_revoke_its_trust()
    {
        var service = Service();
        var first = await service.CompleteAsync(Attempt());
        var device = await service.FindTrustedDeviceAsync(User, first.DeviceToken!);

        await Fail(service, 9, device: device);
        Assert.NotNull(await service.FindTrustedDeviceAsync(User, first.DeviceToken!));
        await Fail(service, 1, device: device);

        Assert.Null(await service.FindTrustedDeviceAsync(User, first.DeviceToken!));
        Assert.DoesNotContain(_store.Counts.Keys, k => k.Scope == LoginProtectionService.AccountUntrustedScope);
    }

    [Fact]
    public async Task A_device_token_belongs_to_one_user()
    {
        var service = Service();
        var first = await service.CompleteAsync(Attempt());

        Assert.Null(await service.FindTrustedDeviceAsync("user-2", first.DeviceToken!));
    }

    [Fact]
    public async Task Password_reset_clears_counters_devices_and_open_codes()
    {
        var service = Service();
        var first = await service.CompleteAsync(Attempt());
        await Fail(service, 5);
        var stepUp = await service.CompleteAsync(Attempt());

        await service.ResetUserAsync(User, "password_reset");

        Assert.Empty(_store.Counts.Keys.Where(k => k.User == User));
        Assert.Null(await service.FindTrustedDeviceAsync(User, first.DeviceToken!));
        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await service.CompleteAsync(Attempt(stepUpId: stepUp.ChallengeId.ToString(), code: _mail.LastCode))).Kind);
    }

    [Fact]
    public async Task A_broken_store_never_blocks_a_login()
    {
        _store.Broken = true;
        var service = Service();

        await service.RecordFailedPasswordAsync(User, Email, Source, null);
        var outcome = await service.CompleteAsync(Attempt(admin: true));

        Assert.Equal(LoginOutcome.Allowed(), outcome);
        Assert.Contains(_log.Entries, e => e.Level == LogLevel.Error && e.Message.StartsWith("login_protection_store_failed Step=complete"));
    }

    [Fact]
    public async Task A_user_without_email_is_let_in_and_flagged_instead_of_stranded()
    {
        var service = Service();
        await Fail(service, 5);

        Assert.Equal(LoginOutcomeKind.Allowed, (await service.CompleteAsync(Attempt(email: ""))).Kind);
        Assert.Contains(_log.Entries, e => e.Level == LogLevel.Error && e.Message.StartsWith("login_step_up_unavailable"));
    }

    [Fact]
    public async Task Cleanup_keeps_every_live_window()
    {
        await Service().CleanupAsync();

        Assert.Equal(new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero), _store.LastCleanupThrottleBefore);
    }

    [Theory]
    [InlineData(LoginProtectionMode.Enforce, true)]
    [InlineData((LoginProtectionMode)7, false)]
    public void Options_validation_rejects_unknown_modes(LoginProtectionMode mode, bool valid) =>
        Assert.Equal(valid, new LoginProtectionOptions { Mode = mode }.IsValid());

    [Fact]
    public void Options_validation_rejects_non_positive_limits() =>
        Assert.False(new LoginProtectionOptions { DeviceFailureLimit = 0 }.IsValid());

    [Fact]
    public void Codes_are_six_digits_and_tokens_are_long_and_url_safe()
    {
        Assert.All(Enumerable.Range(0, 100).Select(_ => LoginSecrets.NewCode()), c => Assert.Matches("^[0-9]{6}$", c));
        Assert.All(Enumerable.Range(0, 20).Select(_ => LoginSecrets.NewDeviceToken()), t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
    }
}
