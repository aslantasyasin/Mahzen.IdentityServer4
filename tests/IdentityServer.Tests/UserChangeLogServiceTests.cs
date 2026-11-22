using IdentityServer.Data;
using IdentityServer.Services.User;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Tests;

public class UserChangeLogServiceTests
{
    [Fact]
    public async Task A_password_event_is_logged_although_both_values_are_masked()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new CustomDbContext(new DbContextOptionsBuilder<CustomDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await using var provider = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() })
            .BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();

        await new UserChangeLogService(db, scopes).LogChangeAsync("user-1", "Password", "***", "***", "user-1");

        var log = Assert.Single(await db.UserChangeLogs.ToListAsync());
        Assert.Equal("Password", log.FieldName);
        Assert.Equal("user-1", log.UserId);
        Assert.Equal(("***", "***"), (log.OldValue, log.NewValue));
    }
}
