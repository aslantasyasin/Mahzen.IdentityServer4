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
    private const string Source = "client:ip_1_1_1_1";

    private sealed class MemoryStore : ILoginThrottleStore
    {
        public Dictionary<(string User, string Scope, DateTimeOffset Window), int> Counts { get; } = new();
        public bool Broken { get; set; }

        public Task<int> IncrementAsync(string userId, string scope, DateTimeOffset windowStart)
        {
            if (Broken) throw new InvalidOperationException("db down");
            var key = (userId, scope, windowStart);
            Counts[key] = Counts.GetValueOrDefault(key) + 1;
            return Task.FromResult(Counts[key]);
        }

        public Task<int> CountAsync(string userId, string scope, DateTimeOffset windowStart)
        {
            if (Broken) throw new InvalidOperationException("db down");
            return Task.FromResult(Counts.GetValueOrDefault((userId, scope, windowStart)));
        }
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 21, 10, 14, 59, TimeSpan.Zero);
    }

    private sealed class ListLogger : ILogger<LoginProtectionService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private readonly MemoryStore _store = new();
    private readonly FixedClock _clock = new();
    private readonly ListLogger _log = new();

    private LoginProtectionService Service(LoginProtectionMode mode = LoginProtectionMode.Shadow) =>
        new(_store, Options.Create(new LoginProtectionOptions { Mode = mode }), _log, _clock);

    private IEnumerable<string> StepUps() => _log.Entries.Select(e => e.Message).Where(m => m.StartsWith("login_step_up_required"));

    [Fact]
    public async Task Off_mode_touches_nothing()
    {
        var service = Service(LoginProtectionMode.Off);
        await service.RecordFailedPasswordAsync(User, Source);
        await service.EvaluateSuccessfulPasswordAsync(User, Source, isAdmin: true);

        Assert.Empty(_store.Counts);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task A_failure_counts_for_the_source_and_the_account_in_their_own_windows()
    {
        await Service().RecordFailedPasswordAsync(User, Source);

        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
        Assert.Equal(1, _store.Counts[(User, LoginProtectionService.AccountUntrustedScope, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
    }

    [Fact]
    public async Task Source_window_rolls_over_at_its_boundary()
    {
        var service = Service();
        await service.RecordFailedPasswordAsync(User, Source);
        _clock.UtcNow = _clock.UtcNow.AddSeconds(1);
        await service.RecordFailedPasswordAsync(User, Source);

        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
        Assert.Equal(1, _store.Counts[(User, "source:" + Source, new DateTimeOffset(2026, 11, 21, 10, 15, 0, TimeSpan.Zero))]);
        Assert.Equal(2, _store.Counts[(User, LoginProtectionService.AccountUntrustedScope, new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero))]);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public async Task Step_up_is_needed_from_the_fifth_source_failure(int failures, bool expected)
    {
        var service = Service();
        for (var i = 0; i < failures; i++) await service.RecordFailedPasswordAsync(User, Source);
        await service.EvaluateSuccessfulPasswordAsync(User, Source, isAdmin: false);

        Assert.Equal(expected, StepUps().Any(m => m.Contains("Reason=source")));
    }

    [Fact]
    public async Task Failures_from_another_source_do_not_affect_this_source()
    {
        var service = Service();
        for (var i = 0; i < 5; i++) await service.RecordFailedPasswordAsync(User, "client:ip_6_6_6_6");
        await service.EvaluateSuccessfulPasswordAsync(User, Source, isAdmin: false);

        Assert.Empty(StepUps());
    }

    [Fact]
    public async Task Account_threshold_counts_failures_across_sources_and_is_logged_once()
    {
        var service = Service();
        for (var i = 0; i < 21; i++) await service.RecordFailedPasswordAsync(User, $"client:ip_{i}");
        await service.EvaluateSuccessfulPasswordAsync(User, Source, isAdmin: false);

        Assert.Single(_log.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("login_account_threshold_reached"));
        Assert.Contains(StepUps(), m => m.Contains("Reason=account"));
    }

    [Fact]
    public async Task Admin_always_needs_step_up_and_users_below_thresholds_do_not()
    {
        await Service().EvaluateSuccessfulPasswordAsync(User, Source, isAdmin: true);
        await Service().EvaluateSuccessfulPasswordAsync("user-2", Source, isAdmin: false);

        var single = Assert.Single(StepUps());
        Assert.Contains("Reason=admin", single);
    }

    [Fact]
    public async Task A_broken_store_never_throws_into_the_login()
    {
        _store.Broken = true;
        var service = Service();

        await service.RecordFailedPasswordAsync(User, Source);
        await service.EvaluateSuccessfulPasswordAsync(User, Source, isAdmin: false);

        Assert.Equal(2, _log.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.StartsWith("login_protection_store_failed")));
    }

    [Theory]
    [InlineData(LoginProtectionMode.Shadow, true)]
    [InlineData((LoginProtectionMode)7, false)]
    public void Options_validation_rejects_unknown_modes(LoginProtectionMode mode, bool valid) =>
        Assert.Equal(valid, new LoginProtectionOptions { Mode = mode }.IsValid());

    [Fact]
    public void Options_validation_rejects_non_positive_limits() =>
        Assert.False(new LoginProtectionOptions { SourceFailureLimit = 0 }.IsValid());
}
