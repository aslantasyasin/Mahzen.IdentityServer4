using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.Dto.User;
using IdentityServer.Services.Login;
using IdentityServer.Services.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityServer.Tests;

public class UpdateEmailPostgresTests
{
    private const string ConnectionVariable = "MAHZEN_REPRO_PG";
    private const string Password = "DogruSifre1!";

    private sealed class NoMail : ILoginMailer
    {
        public Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model) => Task.FromResult(true);
    }

    private sealed class FixedHttpContext : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static async Task<(ServiceProvider Provider, string UserId)> Setup(string connectionString)
    {
        var userId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: new[] { new Claim("sub", userId) }));
        context.Request.Headers["Authorization"] = $"Bearer {jwt}";

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHttpContextAccessor>(new FixedHttpContext { HttpContext = context })
            .AddDbContext<CustomDbContext>(o => o.UseNpgsql(connectionString))
            .AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connectionString));
        services.AddIdentity<ApplicationUser, ApplicationRole>(o => { o.Password.RequireDigit = false; o.Password.RequireNonAlphanumeric = false; o.Password.RequireUppercase = false; })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.AddDataProtection();
        var provider = services.BuildServiceProvider();

        using var setup = provider.CreateScope();
        var created = await setup.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(new ApplicationUser { Id = userId, UserName = $"{userId}@ornek.com", Email = $"{userId}@ornek.com", IsActive = true }, Password);
        Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(e => e.Description)));
        return (provider, userId);
    }

    private static (UserService Service, CustomDbContext Db) Build(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<CustomDbContext>();
        var work = new LoginProtectionServiceTests.ImmediateWork();
        var protection = new LoginProtectionService(new LoginProtectionStore(db), new NoMail(), work, new LoginFallbackLimiter(),
            Options.Create(new LoginProtectionOptions { Mode = LoginProtectionMode.Shadow }), NullLogger<LoginProtectionService>.Instance, new SystemClock());
        work.Service = protection;
        var scopes = sp.GetRequiredService<IServiceScopeFactory>();
        var service = new UserService(sp.GetRequiredService<UserManager<ApplicationUser>>(), null!, scopes, null!, null!, null!,
            new UserChangeLogService(db, scopes), null!, NullLogger<UserService>.Instance, null!, protection, new NoMail());
        return (service, db);
    }

    [Fact]
    public async Task Changing_the_email_after_the_attempt_counters_commits()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrEmpty(connectionString)) return;

        var (provider, userId) = await Setup(connectionString);
        using var scope = provider.CreateScope();
        var (service, db) = Build(scope.ServiceProvider);
        var newEmail = $"yeni-{userId}@ornek.com";

        var result = await service.UpdateEmailAsync(new UpdateEmailRequestDto { Email = newEmail, CurrentPassword = Password });

        Assert.False(result.HasError, string.Join(" | ", result.Errors));
        Assert.Equal(newEmail, (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId)).Email);
        Assert.True(await db.UserChangeLogs.AnyAsync(l => l.UserId == userId && l.FieldName == "Email"));
    }

    [Fact]
    public async Task Changing_the_password_runs_the_counters_and_the_reset()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrEmpty(connectionString)) return;

        var (provider, userId) = await Setup(connectionString);
        using var scope = provider.CreateScope();
        var (service, db) = Build(scope.ServiceProvider);

        var wrong = await service.ChangePasswordAsync(userId, "YanlisSifre1!", "YeniSifre123!");
        var right = await service.ChangePasswordAsync(userId, Password, "YeniSifre123!");

        Assert.Contains("PasswordMismatch", wrong.Errors);
        Assert.False(right.HasError, string.Join(" | ", right.Errors));
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT count(*) FROM ids4.\"LoginThrottle\" WHERE \"UserId\" = @u";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "u";
            parameter.Value = userId;
            command.Parameters.Add(parameter);
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
