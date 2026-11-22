using System.Data;
using IdentityServer.Data;
using IdentityServer.Services.Login;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Tests;

public class LoginProtectionStoreConnectionTests
{
    [Fact]
    public async Task The_store_releases_the_context_connection_after_each_call()
    {
        var options = new DbContextOptionsBuilder<CustomDbContext>().UseSqlite("DataSource=:memory:").Options;
        await using var db = new CustomDbContext(options);
        var store = new LoginProtectionStore(db);

        await Assert.ThrowsAnyAsync<Exception>(() => store.IncrementAsync("user-1", "password-checks", DateTimeOffset.UtcNow));

        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }
}
