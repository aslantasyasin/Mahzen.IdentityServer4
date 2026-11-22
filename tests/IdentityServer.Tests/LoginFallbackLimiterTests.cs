using IdentityServer.Services.Login;

namespace IdentityServer.Tests;

public class LoginFallbackLimiterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_full_cache_never_lets_an_account_past_the_limit()
    {
        var options = new LoginProtectionOptions();
        using var limiter = new LoginFallbackLimiter();
        for (var i = 0; i < LoginFallbackLimiter.MaxEntries; i++)
            limiter.RecordAttempt("filler", $"ip:{i}", options.AccountWindow, Now);

        var last = 0;
        for (var i = 0; i <= options.AccountFailureLimit; i++)
            last = limiter.RecordAttempt("victim", LoginProtectionService.AccountUntrustedScope, options.AccountWindow, Now);

        Assert.True(last > options.AccountFailureLimit, $"attempt {options.AccountFailureLimit + 1} counted as {last}");
    }

    [Fact]
    public void An_account_is_counted_attempt_by_attempt_while_there_is_room()
    {
        var options = new LoginProtectionOptions();
        using var limiter = new LoginFallbackLimiter();

        var counts = Enumerable.Range(0, 3)
            .Select(_ => limiter.RecordAttempt("victim", LoginProtectionService.AccountUntrustedScope, options.AccountWindow, Now))
            .ToArray();

        Assert.Equal(new[] { 1, 2, 3 }, counts);
    }
}
