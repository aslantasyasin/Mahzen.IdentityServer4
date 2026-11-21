using System.Net;
using IdentityServer.Services.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
        public List<(long Id, string User, string Hash, DateTimeOffset LastUsed, DateTimeOffset Expires, DateTimeOffset? Revoked, string? Reason)> Devices { get; } = new();
        public Dictionary<Guid, (string User, string Hash, int Attempts, int Max, DateTimeOffset Created, DateTimeOffset Expires, DateTimeOffset? LastSent, DateTimeOffset? Used)> Challenges { get; } = new();
        public Dictionary<(string User, string Kind), DateTimeOffset> Notices { get; } = new();
        public bool Broken { get; set; }
        public int Calls { get; private set; }

        private void Check() { Calls++; if (Broken) throw new InvalidOperationException("db down"); }

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
            lock (_gate) Devices.Add((++_nextDevice, userId, tokenHash, now, expiresAt, null, null));
            return Task.CompletedTask;
        }

        public Task TouchDeviceAsync(long deviceId, DateTimeOffset now, DateTimeOffset expiresAt)
        {
            Check();
            lock (_gate) { var i = Devices.FindIndex(x => x.Id == deviceId); Devices[i] = Devices[i] with { LastUsed = now, Expires = expiresAt }; }
            return Task.CompletedTask;
        }

        public Task RevokeDeviceAsync(long deviceId, DateTimeOffset now, string reason)
        {
            Check();
            lock (_gate) { var i = Devices.FindIndex(x => x.Id == deviceId); Devices[i] = Devices[i] with { Revoked = now, Reason = reason }; }
            return Task.CompletedTask;
        }

        public Task RevokeExcessDevicesAsync(string userId, int keep, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                var excess = Devices.Where(d => d.User == userId && d.Revoked is null).OrderByDescending(d => d.LastUsed).ThenByDescending(d => d.Id).Skip(keep).Select(d => d.Id).ToList();
                foreach (var id in excess) { var i = Devices.FindIndex(x => x.Id == id); Devices[i] = Devices[i] with { Revoked = now, Reason = "limit" }; }
            }
            return Task.CompletedTask;
        }

        public Task<LoginChallenge> FindOpenChallengeAsync(string userId, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                var c = Challenges.Where(x => x.Value.User == userId && x.Value.Used is null && x.Value.Expires > now)
                    .OrderByDescending(x => x.Value.Created).Select(x => Map(x.Key)).FirstOrDefault();
                return Task.FromResult(c!);
            }
        }

        public Task<LoginChallenge> GetChallengeAsync(Guid id)
        {
            Check();
            lock (_gate) return Task.FromResult(Challenges.ContainsKey(id) ? Map(id) : null!);
        }

        private LoginChallenge Map(Guid id)
        {
            var c = Challenges[id];
            return new LoginChallenge(id, c.User, c.Hash, c.Expires, c.LastSent, c.Used is not null);
        }

        public Task CreateChallengeAsync(Guid id, string userId, string codeHash, int maxAttempts, DateTimeOffset now, DateTimeOffset expiresAt)
        {
            Check();
            lock (_gate) Challenges[id] = (userId, codeHash, 0, maxAttempts, now, expiresAt, null, null);
            return Task.CompletedTask;
        }

        public Task<bool> TryRotateChallengeCodeAsync(Guid id, string codeHash, DateTimeOffset now, DateTimeOffset sentBefore)
        {
            Check();
            lock (_gate)
            {
                if (!Challenges.TryGetValue(id, out var c) || c.Used is not null || c.Expires <= now || c.LastSent > sentBefore) return Task.FromResult(false);
                Challenges[id] = c with { Hash = codeHash, LastSent = now, Attempts = 0 };
                return Task.FromResult(true);
            }
        }

        public Task<bool> TryConsumeChallengeAttemptAsync(Guid id, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                if (!Challenges.TryGetValue(id, out var c) || c.Used is not null || c.Expires <= now || c.Attempts >= c.Max) return Task.FromResult(false);
                Challenges[id] = c with { Attempts = c.Attempts + 1 };
                return Task.FromResult(true);
            }
        }

        public Task<bool> TryCompleteChallengeAsync(Guid id, DateTimeOffset now)
        {
            Check();
            lock (_gate)
            {
                if (!Challenges.TryGetValue(id, out var c) || c.Used is not null) return Task.FromResult(false);
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

        public Task MarkFlagAsync(string userId, string kind, DateTimeOffset now)
        {
            Check();
            lock (_gate) Notices[(userId, kind)] = now;
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetFlagAsync(string userId, string kind)
        {
            Check();
            lock (_gate) return Task.FromResult(Notices.TryGetValue((userId, kind), out var at) ? at : (DateTimeOffset?)null);
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
                Notices.Remove((userId, "under-attack"));
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
        public string? LastCodeOrNull() => Sent.LastOrDefault(m => m.Template == LoginMailTemplates.Code).Model?["Code"];
        public int Count(string template) => Sent.Count(m => m.Template == template);
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = Start;
    }

    internal sealed class ImmediateWork : ILoginWork
    {
        public LoginProtectionService? Service { get; set; }
        public bool Defer { get; set; }
        public List<string> Deferred { get; } = new();

        public void Enqueue(string name, Func<LoginProtectionService, Task> job)
        {
            if (Defer) { Deferred.Add(name); return; }
            job(Service!).GetAwaiter().GetResult();
        }
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
    private readonly LoginFallbackLimiter _fallback = new();
    private readonly ImmediateWork _work = new();

    private LoginProtectionService Service(LoginProtectionMode mode = LoginProtectionMode.Enforce)
    {
        var service = new LoginProtectionService(_store, _mail, _work, _fallback, Options.Create(new LoginProtectionOptions { Mode = mode }), _log, _clock);
        _work.Service = service;
        return service;
    }

    private static async Task<LoginOutcome> Login(LoginProtectionService service, bool passwordOk, string source = Source, long? device = null,
        bool admin = false, string? stepUpId = null, string? code = null, string user = User, string email = Email)
    {
        var gate = await service.EnterAsync(user, email, source, device, admin);
        if (!passwordOk) await service.RecordFailedPasswordAsync(user, source, device);
        return await service.CompleteAsync(new LoginAttempt(user, email, source, device, admin, stepUpId!, code!), gate, passwordOk);
    }

    private static async Task Fail(LoginProtectionService service, int times, string source = Source, long? device = null)
    {
        for (var i = 0; i < times; i++) await Login(service, false, source, device);
    }

    private static Task<LoginOutcome> Verify(LoginProtectionService service, LoginOutcome stepUp, string code, bool passwordOk = true, string user = User) =>
        Login(service, passwordOk, stepUpId: stepUp.ChallengeId.ToString(), code: code, user: user);

    private IEnumerable<string> Messages(string prefix) => _log.Entries.Select(e => e.Message).Where(m => m.StartsWith(prefix));

    [Fact]
    public async Task Off_mode_touches_nothing()
    {
        var service = Service(LoginProtectionMode.Off);
        Assert.Equal(LoginOutcomeKind.Rejected, (await Login(service, false)).Kind);
        Assert.Equal(LoginOutcome.Allowed(), await Login(service, true, admin: true));

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
    public async Task Failed_attempts_are_labelled(bool locked, string label)
    {
        await Service(LoginProtectionMode.Shadow).RecordFailedPasswordAsync(User, Source, null, whileLockedOut: locked);

        Assert.Contains(Messages("login_failed_password"), m => m.Contains(label));
    }

    [Fact]
    public async Task A_clean_login_is_allowed_and_gets_a_hashed_device_token_and_a_new_device_notice()
    {
        var outcome = await Login(Service(), true);

        Assert.Equal(LoginOutcomeKind.Allowed, outcome.Kind);
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

        Assert.Equal(expected, (await Login(service, true)).Kind);
    }

    [Fact]
    public async Task Under_the_gate_a_wrong_and_a_right_password_get_the_same_answer_and_only_the_right_one_sends_a_code()
    {
        var service = Service();
        await Fail(service, 5);

        var wrong = await Login(service, false);
        Assert.Equal(0, _mail.Count(LoginMailTemplates.Code));
        var right = await Login(service, true);

        Assert.Equal(LoginOutcomeKind.StepUpRequired, wrong.Kind);
        Assert.Equal(wrong, right);
        Assert.Equal(1, _mail.Count(LoginMailTemplates.Code));
    }

    [Fact]
    public async Task The_challenge_id_only_changes_with_time_not_with_the_password()
    {
        var service = Service();
        await Fail(service, 5);
        var x = await Login(service, false);

        for (var i = 0; i < 5; i++) await Verify(service, x, "000000" == _mail.LastCodeOrNull() ? "111111" : "000000");
        Assert.Equal(x.ChallengeId, (await Login(service, false)).ChallengeId);

        for (var i = 0; i < 5; i++) await Verify(service, x, "000000", passwordOk: false);
        Assert.Equal(x.ChallengeId, (await Login(service, false)).ChallengeId);

        _clock.UtcNow = Start.AddMinutes(6);
        Assert.NotEqual(x.ChallengeId, (await Login(service, false)).ChallengeId);
    }

    [Fact]
    public async Task Under_the_gate_right_and_wrong_passwords_cost_the_same_store_calls()
    {
        var service = Service();
        await Fail(service, 5);
        await Login(service, false);
        _work.Defer = true;

        var before = _store.Calls;
        await Login(service, false);
        var wrong = _store.Calls - before;
        before = _store.Calls;
        await Login(service, true);
        var right = _store.Calls - before;

        Assert.Equal(wrong, right);
        Assert.Contains("send_code", _work.Deferred);
    }

    [Fact]
    public async Task Verifying_with_a_wrong_password_costs_the_same_as_a_wrong_code()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await Login(service, false);
        _work.Defer = true;

        var before = _store.Calls;
        await Verify(service, stepUp, "000000", passwordOk: false);
        var wrongPassword = _store.Calls - before;
        before = _store.Calls;
        await Verify(service, stepUp, "000000");
        var wrongCode = _store.Calls - before;

        Assert.Equal(wrongPassword, wrongCode);
    }

    [Fact]
    public async Task A_made_up_step_up_id_reveals_nothing_about_the_password()
    {
        var service = Service();
        var fake = Guid.NewGuid().ToString();

        var wrong = await Login(service, false, stepUpId: fake, code: "123456");
        var right = await Login(service, true, stepUpId: fake, code: "123456");

        Assert.Equal(LoginOutcomeKind.StepUpFailed, wrong.Kind);
        Assert.Equal(wrong, right);
    }

    [Fact]
    public async Task After_the_account_threshold_new_devices_need_a_code_for_a_day_not_just_two_windows()
    {
        var service = Service();
        for (var i = 0; i < 21; i++) await Login(service, false, source: $"client:ip_{i}");

        _clock.UtcNow = Start.AddHours(3);
        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await Login(service, true, source: "client:ip_new")).Kind);

        _clock.UtcNow = Start.AddHours(25);
        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true, source: "client:ip_newer")).Kind);
    }

    [Fact]
    public async Task Shadow_only_logs_the_step_up_lets_the_user_in_and_never_mails()
    {
        var service = Service(LoginProtectionMode.Shadow);
        for (var i = 0; i < 25; i++) await Login(service, false, source: $"client:ip_{i}");

        var outcome = await Login(service, true);
        await Login(service, true, admin: true, user: "admin-1");

        Assert.Equal(LoginOutcomeKind.Allowed, outcome.Kind);
        Assert.Contains(Messages("login_step_up_required"), m => m.Contains("Mode=Shadow"));
        Assert.Empty(_mail.Sent);
        Assert.NotEmpty(_store.Devices);
    }

    [Fact]
    public async Task Failures_split_across_a_window_boundary_still_count()
    {
        var service = Service();
        await Fail(service, 3);
        _clock.UtcNow = Start.AddSeconds(1);
        await Fail(service, 2);

        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await Login(service, true)).Kind);
    }

    [Fact]
    public async Task Source_failures_older_than_two_windows_are_forgotten()
    {
        var service = Service();
        await Fail(service, 5);
        _clock.UtcNow = Start.AddMinutes(16);

        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true)).Kind);
    }

    [Fact]
    public async Task Failures_from_another_source_do_not_affect_this_source()
    {
        var service = Service();
        await Fail(service, 5, source: "client:ip_6_6_6_6");

        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true)).Kind);
    }

    [Fact]
    public async Task The_attack_notice_counts_failures_split_across_windows_and_goes_once_a_day()
    {
        var service = Service();
        for (var i = 0; i < 19; i++) await Login(service, false, source: $"client:ip_{i}");
        _clock.UtcNow = Start.AddHours(1);
        Assert.Equal(0, _mail.Count(LoginMailTemplates.AttackNotice));
        await Login(service, false, source: "client:ip_x");
        _clock.UtcNow = Start.AddHours(2);
        for (var i = 0; i < 25; i++) await Login(service, false, source: $"client:ip_{i}");

        Assert.Equal(1, _mail.Count(LoginMailTemplates.AttackNotice));
    }

    [Fact]
    public async Task Admin_needs_a_code_on_an_untrusted_device_but_not_on_a_trusted_one()
    {
        var service = Service();
        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await Login(service, true, admin: true)).Kind);

        await _store.AddDeviceAsync(User, LoginSecrets.HashDeviceToken("admin-device"), Start, Start.AddDays(1));
        var device = await service.FindTrustedDeviceAsync(User, "admin-device");
        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true, admin: true, device: device)).Kind);
    }

    [Fact]
    public async Task Principle_attacker_cannot_lock_the_owner_out()
    {
        var service = Service();
        var first = await Login(service, true);
        var ownerDevice = await service.FindTrustedDeviceAsync(User, first.DeviceToken!);

        await Fail(service, 100);
        for (var i = 0; i < 100; i++) await Login(service, false, source: $"client:ip_{i}");

        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true, device: ownerDevice)).Kind);

        var stepUp = await Login(service, true);
        Assert.Equal(LoginOutcomeKind.StepUpRequired, stepUp.Kind);
        var verified = await Verify(service, stepUp, _mail.LastCode);
        Assert.Equal(LoginOutcomeKind.Allowed, verified.Kind);
        Assert.NotNull(verified.DeviceToken);
    }

    [Fact]
    public async Task Code_attempts_are_spent_before_comparing_so_the_right_code_fails_after_five_wrong_ones()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await Login(service, true);
        var code = _mail.LastCode;

        for (var i = 0; i < 5; i++)
            Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, stepUp, code == "000000" ? "111111" : "000000")).Kind);

        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, stepUp, code)).Kind);
    }

    [Fact]
    public async Task A_right_code_with_a_wrong_password_fails_and_does_not_spend_the_code()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await Login(service, true);
        var code = _mail.LastCode;

        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, stepUp, code, passwordOk: false)).Kind);
        Assert.Equal(LoginOutcomeKind.Allowed, (await Verify(service, stepUp, code)).Kind);
    }

    [Fact]
    public async Task A_challenge_cannot_be_used_by_another_user_or_twice()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await Login(service, true);
        var code = _mail.LastCode;

        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, stepUp, code, user: "user-2")).Kind);
        Assert.Equal(LoginOutcomeKind.Allowed, (await Verify(service, stepUp, code)).Kind);
        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, stepUp, code)).Kind);
    }

    [Fact]
    public async Task Repeated_logins_reuse_the_challenge_and_resend_only_after_the_cooldown()
    {
        var service = Service();
        await Fail(service, 5);
        var first = await Login(service, true);
        var firstCode = _mail.LastCode;

        var again = await Login(service, true);
        Assert.Equal(first.ChallengeId, again.ChallengeId);
        Assert.Equal(1, _mail.Count(LoginMailTemplates.Code));

        _clock.UtcNow = Start.AddSeconds(61);
        var resent = await Login(service, true);
        Assert.Equal(first.ChallengeId, resent.ChallengeId);
        Assert.Equal(2, _mail.Count(LoginMailTemplates.Code));
        var newCode = _mail.LastCode;

        if (newCode != firstCode)
            Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, first, firstCode)).Kind);
        Assert.Equal(LoginOutcomeKind.Allowed, (await Verify(service, first, newCode)).Kind);
    }

    [Fact]
    public async Task Code_mails_are_capped_per_hour()
    {
        var service = Service();
        await Fail(service, 5);
        for (var i = 0; i < 8; i++)
        {
            _clock.UtcNow = Start.AddSeconds(61 * i);
            await Login(service, true);
        }

        Assert.Equal(5, _mail.Count(LoginMailTemplates.Code));
        Assert.NotEmpty(Messages("login_step_up_send_budget_exhausted"));
    }

    [Fact]
    public async Task Wrong_codes_are_capped_per_account_across_challenges()
    {
        var service = Service();
        await Fail(service, 5);
        for (var round = 0; round < 3; round++)
        {
            _clock.UtcNow = Start.AddSeconds(61 * round);
            var stepUp = await Login(service, true);
            for (var i = 0; i < 5; i++) await Verify(service, stepUp, "000000" == _mail.LastCode ? "111111" : "000000");
        }

        _clock.UtcNow = Start.AddSeconds(200);
        var last = await Login(service, true);
        Assert.Equal(LoginOutcomeKind.StepUpRequired, last.Kind);
        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, last, _mail.LastCode)).Kind);
        Assert.Contains(Messages("login_step_up_failed"), m => m.Contains("Reason=too_many_wrong_codes"));
    }

    [Fact]
    public async Task Ten_wrong_passwords_on_a_trusted_device_revoke_its_trust_without_touching_the_account_counter()
    {
        var service = Service();
        var first = await Login(service, true);
        var device = await service.FindTrustedDeviceAsync(User, first.DeviceToken!);
        var accountBefore = _store.Counts.Where(k => k.Key.Scope == LoginProtectionService.AccountUntrustedScope).Sum(k => k.Value);

        await Fail(service, 9, device: device);
        Assert.NotNull(await service.FindTrustedDeviceAsync(User, first.DeviceToken!));
        await Fail(service, 1, device: device);

        Assert.Null(await service.FindTrustedDeviceAsync(User, first.DeviceToken!));
        Assert.Equal(accountBefore, _store.Counts.Where(k => k.Key.Scope == LoginProtectionService.AccountUntrustedScope).Sum(k => k.Value));
    }

    [Fact]
    public async Task A_device_token_belongs_to_one_user()
    {
        var first = await Login(Service(), true);

        Assert.Null(await Service().FindTrustedDeviceAsync("user-2", first.DeviceToken!));
    }

    [Fact]
    public async Task A_user_keeps_at_most_ten_devices_and_the_least_recently_used_goes()
    {
        var service = Service();
        var tokens = new List<string>();
        for (var i = 0; i < 11; i++)
        {
            _clock.UtcNow = Start.AddMinutes(i);
            tokens.Add((await Login(service, true, source: $"client:ip_{i}")).DeviceToken!);
        }

        Assert.Null(await service.FindTrustedDeviceAsync(User, tokens[0]));
        Assert.NotNull(await service.FindTrustedDeviceAsync(User, tokens[10]));
        Assert.Equal(10, _store.Devices.Count(d => d.Revoked is null));
    }

    [Fact]
    public async Task Password_reset_clears_counters_devices_and_open_codes()
    {
        var service = Service();
        var first = await Login(service, true);
        await Fail(service, 5);
        var stepUp = await Login(service, true);

        await service.ResetUserAsync(User, "password_reset");

        Assert.Empty(_store.Counts.Keys.Where(k => k.User == User));
        Assert.Null(await service.FindTrustedDeviceAsync(User, first.DeviceToken!));
        Assert.Equal(LoginOutcomeKind.StepUpFailed, (await Verify(service, stepUp, _mail.LastCode)).Kind);
    }

    [Fact]
    public async Task A_store_failure_while_verifying_a_code_never_lets_the_user_skip_it()
    {
        var service = Service();
        await Fail(service, 5);
        var stepUp = await Login(service, true);
        var code = _mail.LastCode;
        var gate = await service.EnterAsync(User, Email, Source, null, false);
        _store.Broken = true;

        var outcome = await service.CompleteAsync(new LoginAttempt(User, Email, Source, null, false, stepUp.ChallengeId.ToString()!, code), gate, true);

        Assert.Equal(LoginOutcomeKind.StepUpFailed, outcome.Kind);
    }

    [Fact]
    public async Task A_broken_store_keeps_customers_in_but_limits_guessing_in_memory()
    {
        _store.Broken = true;
        var service = Service();

        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true)).Kind);
        await Fail(service, 5);

        Assert.Equal(LoginOutcomeKind.Rejected, (await Login(service, true)).Kind);
        Assert.Equal(LoginOutcomeKind.Allowed, (await Login(service, true, source: "client:ip_other")).Kind);
        Assert.Equal(LoginOutcomeKind.TemporarilyUnavailable, (await Login(service, true, admin: true, source: "client:ip_admin")).Kind);
        Assert.Contains(_log.Entries, e => e.Level == LogLevel.Error && e.Message.StartsWith("login_protection_store_failed"));
    }

    [Fact]
    public async Task A_user_without_email_is_flagged()
    {
        var service = Service();
        await Fail(service, 5);

        Assert.Equal(LoginOutcomeKind.StepUpRequired, (await Login(service, true, email: "")).Kind);
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
        Assert.False(new LoginProtectionOptions { MaxDevicesPerUser = 0 }.IsValid());

    [Fact]
    public void Codes_are_six_digits_and_tokens_are_long_and_url_safe()
    {
        Assert.All(Enumerable.Range(0, 100).Select(_ => LoginSecrets.NewCode()), c => Assert.Matches("^[0-9]{6}$", c));
        Assert.All(Enumerable.Range(0, 20).Select(_ => LoginSecrets.NewDeviceToken()), t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
    }
}

public class TrustedProxyNetworkTests
{
    private static IEnumerable<Microsoft.AspNetCore.HttpOverrides.IPNetwork> Networks(params string[] cidrs) =>
        IdentityServer.Startup.TrustedProxyNetworks(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(cidrs.Select((c, i) => new KeyValuePair<string, string?>($"ForwardedHeaders:KnownNetworks:{i}", c)))
            .Build()).ToList();

    [Fact]
    public void Configured_networks_contain_cluster_addresses_but_not_internet_ones()
    {
        var networks = Networks("10.244.0.0/16", "127.0.0.0/8");

        Assert.Contains(networks, n => n.Contains(IPAddress.Parse("10.244.0.202")));
        Assert.Contains(networks, n => n.Contains(IPAddress.Parse("127.0.0.1")));
        Assert.DoesNotContain(networks, n => n.Contains(IPAddress.Parse("95.70.1.2")));
    }

    [Fact]
    public void An_empty_list_keeps_the_old_trust_everyone_behaviour() => Assert.Empty(Networks());

    [Fact]
    public void A_malformed_entry_stops_startup() =>
        Assert.Throws<InvalidOperationException>(() => Networks("10.244.0.0"));

    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("::1/129")]
    public void A_prefix_out_of_range_stops_startup(string cidr) =>
        Assert.Throws<InvalidOperationException>(() => Networks(cidr));
}
