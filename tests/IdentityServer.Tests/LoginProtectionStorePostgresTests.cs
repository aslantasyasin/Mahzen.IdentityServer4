using IdentityServer.Data;
using IdentityServer.Services.Login;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Tests;

public class LoginProtectionStorePostgresTests
{
    private const string ConnectionVariable = "MAHZEN_REPRO_PG";

    [Fact]
    public async Task Cleanup_keeps_the_daily_counter_longer_than_the_others()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrEmpty(connectionString)) return;

        await using var db = new CustomDbContext(new DbContextOptionsBuilder<CustomDbContext>().UseNpgsql(connectionString).Options);
        var store = new LoginProtectionStore(db);
        var userId = Guid.NewGuid().ToString();
        var now = new DateTimeOffset(2000, 1, 3, 18, 0, 0, TimeSpan.Zero);
        var throttleBefore = now.AddHours(-2);
        var dailyThrottleBefore = now.AddHours(-24);

        await store.IncrementAsync(userId, "source:old", throttleBefore.AddHours(-1));
        await store.IncrementAsync(userId, "source:live", throttleBefore.AddHours(1));
        await store.IncrementAsync(userId, LoginProtectionService.PasswordChecksDayScope, throttleBefore.AddHours(-10));
        await store.IncrementAsync(userId, LoginProtectionService.PasswordChecksDayScope, dailyThrottleBefore.AddHours(-12));

        await store.CleanupAsync(now, throttleBefore, dailyThrottleBefore, LoginProtectionService.CleanupBatchSize);

        Assert.Equal(0, await store.CountAsync(userId, "source:old", throttleBefore.AddHours(-1)));
        Assert.Equal(1, await store.CountAsync(userId, "source:live", throttleBefore.AddHours(1)));
        Assert.Equal(1, await store.CountAsync(userId, LoginProtectionService.PasswordChecksDayScope, throttleBefore.AddHours(-10)));
        Assert.Equal(0, await store.CountAsync(userId, LoginProtectionService.PasswordChecksDayScope, dailyThrottleBefore.AddHours(-12)));
    }
}
