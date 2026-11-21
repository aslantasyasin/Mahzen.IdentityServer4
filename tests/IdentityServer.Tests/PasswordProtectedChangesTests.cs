using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Models;
using IdentityServer.Models.Dto.User;
using IdentityServer.Services.Login;
using IdentityServer.Services.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityServer.Tests;

public class PasswordProtectedChangesTests
{
    private const string UserId = "user-1";
    private const string Password = "DogruSifre1!";
    private const string TakenEmail = "dolu@ornek.com";
    private const string NewEmail = "yeni@ornek.com";

    private sealed class NoStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string userName, CancellationToken ct) => Task.CompletedTask;
        public Task<string> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string normalizedName, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser> FindByIdAsync(string userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser> FindByNameAsync(string normalizedUserName, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Users : UserManager<ApplicationUser>
    {
        public readonly ApplicationUser Customer = new() { Id = UserId, UserName = "ali@ornek.com", Email = "ali@ornek.com", IsActive = true };
        public int EmailChanges;
        public int PasswordChecks;
        public bool FailToSave;

        public Users() : base(new NoStore(), null!, null!, null!, null!, null!, null!, null!, null!) { }

        public override Task<ApplicationUser> FindByIdAsync(string userId) => Task.FromResult(userId == UserId ? Customer : null!);
        public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        {
            Interlocked.Increment(ref PasswordChecks);
            return Task.FromResult(password == Password);
        }
        public override Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)
        {
            Interlocked.Increment(ref PasswordChecks);
            return Task.FromResult(currentPassword == Password
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch" }));
        }
        public override Task<ApplicationUser> FindByEmailAsync(string email) =>
            Task.FromResult(email == TakenEmail ? new ApplicationUser { Id = "user-2", Email = TakenEmail } : null!);
        public override Task<string> GenerateChangeEmailTokenAsync(ApplicationUser user, string newEmail) => Task.FromResult("token");
        public override Task<IdentityResult> ChangeEmailAsync(ApplicationUser user, string newEmail, string token)
        {
            EmailChanges++;
            user.Email = newEmail;
            return Task.FromResult(IdentityResult.Success);
        }
        public override Task<IdentityResult> UpdateAsync(ApplicationUser user) =>
            FailToSave ? throw new InvalidOperationException("db") : Task.FromResult(IdentityResult.Success);
    }

    private sealed class NoChangeLog : IUserChangeLogService
    {
        public Task LogChangeAsync(string userId, string fieldName, string oldValue, string newValue, string changedBy) => Task.CompletedTask;
    }

    private sealed class Mailer : ILoginMailer
    {
        public bool Accepts = true;
        public readonly List<(string To, string Template, IReadOnlyDictionary<string, string> Model)> Sent = new();

        public Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model)
        {
            Sent.Add((to, templateKey, model));
            return Task.FromResult(Accepts);
        }
    }

    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly Clock _clock = new();

    private readonly Users _users = new();
    private readonly Mailer _mailer = new();
    private readonly LoginProtectionServiceTests.MemoryStore _store = new();
    private readonly LoginProtectionService _protection;

    public PasswordProtectedChangesTests()
    {
        var work = new LoginProtectionServiceTests.ImmediateWork();
        _protection = new LoginProtectionService(_store, new LoginProtectionServiceTests.RecordingMailer(), work, new LoginFallbackLimiter(),
            Options.Create(new LoginProtectionOptions { Mode = LoginProtectionMode.Shadow }), NullLogger<LoginProtectionService>.Instance, _clock);
        work.Service = _protection;
    }

    private Task<IdentityServer.Models.Base.ApiResponse<bool>> Change(string email) =>
        Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = email, CurrentPassword = Password });

    private UserService Service()
    {
        var context = new DefaultHttpContext();
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: new[] { new Claim("sub", UserId) }));
        context.Request.Headers["Authorization"] = $"Bearer {token}";
        var scopes = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context })
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        return new UserService(_users, null!, scopes, null!, null!, null!, new NoChangeLog(), null!,
            NullLogger<UserService>.Instance, null!, _protection, _mailer);
    }

    [Fact]
    public async Task The_right_password_changes_the_email()
    {
        var result = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = NewEmail, CurrentPassword = Password });

        Assert.False(result.HasError);
        Assert.Equal(NewEmail, _users.Customer.Email);
        var notice = Assert.Single(_mailer.Sent);
        Assert.Equal("ali@ornek.com", notice.To);
        Assert.Equal("email-changed", notice.Template);
        Assert.Equal(new[] { "At" }, notice.Model.Keys);
        Assert.Matches(@"^[0-9]{2}\.[0-9]{2}\.[0-9]{4} [0-9]{2}:[0-9]{2}$", notice.Model["At"]);
    }

    [Fact]
    public async Task Only_three_changes_an_hour_are_allowed()
    {
        for (var i = 1; i <= LoginProtectionService.EmailChangesPerHour; i++)
            Assert.False((await Change($"yeni{i}@ornek.com")).HasError);

        var refused = await Change("bir-tane-daha@ornek.com");

        Assert.True(refused.HasError);
        Assert.Equal("yeni3@ornek.com", _users.Customer.Email);
        Assert.Equal(LoginProtectionService.EmailChangesPerHour, _mailer.Sent.Count);
    }

    [Theory]
    [InlineData("password_change")]
    [InlineData("password_reset")]
    public async Task Resetting_the_password_does_not_reset_the_change_limit(string reason)
    {
        for (var i = 1; i <= LoginProtectionService.EmailChangesPerHour; i++)
            await Change($"yeni{i}@ornek.com");

        await _protection.ResetUserAsync(UserId, reason);

        Assert.True((await Change("bir-tane-daha@ornek.com")).HasError);
    }

    [Theory]
    [InlineData("ali@ornek.com")]
    [InlineData(" ALI@ornek.com ")]
    public async Task The_current_address_is_not_a_change(string email)
    {
        var result = await Change(email);

        Assert.True(result.HasError);
        Assert.DoesNotContain("PasswordMismatch", result.Errors);
        Assert.Equal(0, _users.EmailChanges);
        Assert.Empty(_mailer.Sent);
        Assert.DoesNotContain(_store.Counts.Keys, k => k.Scope == LoginProtectionService.EmailChangesScope);
    }

    private Task<IdentityServer.Models.Base.ApiResponse<bool>> ChangePassword(string current) =>
        Service().ChangePasswordAsync(UserId, current, "YeniSifre1!");

    [Fact]
    public async Task After_five_password_checks_even_the_right_password_is_refused_without_checking()
    {
        for (var i = 0; i < LoginProtectionService.PasswordChecksPerHour; i++)
            Assert.Contains("PasswordMismatch", (await ChangePassword("YanlisSifre1!")).Errors);

        var email = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = NewEmail, CurrentPassword = Password });
        var password = await ChangePassword(Password);

        Assert.True(email.HasError);
        Assert.True(password.HasError);
        Assert.Equal(new[] { "TooManyPasswordChecks" }, email.Errors);
        Assert.Equal(new[] { "TooManyPasswordChecks" }, password.Errors);
        Assert.Equal(LoginProtectionService.PasswordChecksPerHour, _users.PasswordChecks);
        Assert.Equal("ali@ornek.com", _users.Customer.Email);
        Assert.Empty(_mailer.Sent);
    }

    [Fact]
    public async Task Email_and_password_changes_share_the_limit()
    {
        for (var i = 0; i < LoginProtectionService.PasswordChecksPerHour; i++)
            await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = NewEmail, CurrentPassword = "YanlisSifre1!" });

        Assert.True((await ChangePassword(Password)).HasError);
        Assert.Equal(LoginProtectionService.PasswordChecksPerHour, _users.PasswordChecks);
    }

    [Fact]
    public async Task The_limit_does_not_reset_at_the_top_of_the_hour()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 11, 22, 10, 59, 0, TimeSpan.Zero);
        for (var i = 0; i < LoginProtectionService.PasswordChecksPerHour; i++)
            await ChangePassword("YanlisSifre1!");

        _clock.UtcNow = new DateTimeOffset(2026, 11, 22, 11, 1, 0, TimeSpan.Zero);

        Assert.True((await ChangePassword(Password)).HasError);
        Assert.Equal(LoginProtectionService.PasswordChecksPerHour, _users.PasswordChecks);
    }

    [Fact]
    public async Task A_password_reset_lifts_the_limit()
    {
        for (var i = 0; i < LoginProtectionService.PasswordChecksPerHour; i++)
            await ChangePassword("YanlisSifre1!");

        await _protection.ResetUserAsync(UserId, "password_reset");

        Assert.False((await ChangePassword(Password)).HasError);
    }

    [Fact]
    public async Task Concurrent_checks_never_exceed_the_limit()
    {
        var attempts = Enumerable.Range(0, 20).Select(_ => ChangePassword("YanlisSifre1!"));
        await Task.WhenAll(attempts);

        Assert.Equal(LoginProtectionService.PasswordChecksPerHour, _users.PasswordChecks);
    }

    [Fact]
    public async Task A_change_that_fails_to_save_sends_no_notice()
    {
        _users.FailToSave = true;

        var result = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = NewEmail, CurrentPassword = Password });

        Assert.True(result.HasError);
        Assert.Empty(_mailer.Sent);
    }

    [Fact]
    public async Task A_full_mail_queue_does_not_undo_the_change()
    {
        _mailer.Accepts = false;

        var result = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = NewEmail, CurrentPassword = Password });

        Assert.False(result.HasError);
        Assert.Equal(NewEmail, _users.Customer.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("YanlisSifre1!")]
    public async Task A_missing_or_wrong_password_leaves_the_email_alone(string? password)
    {
        var result = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = NewEmail, CurrentPassword = password! });

        Assert.Equal(new[] { "PasswordMismatch" }, result.Errors);
        Assert.Equal(0, _users.EmailChanges);
        Assert.Equal("ali@ornek.com", _users.Customer.Email);
        Assert.Empty(_mailer.Sent);
    }

    [Fact]
    public async Task Without_the_password_a_taken_address_is_not_revealed()
    {
        var result = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = TakenEmail, CurrentPassword = "YanlisSifre1!" });

        Assert.Equal(new[] { "PasswordMismatch" }, result.Errors);
    }

    [Fact]
    public async Task With_the_password_a_taken_address_is_still_refused()
    {
        var result = await Service().UpdateEmailAsync(new UpdateEmailRequestDto { Email = TakenEmail, CurrentPassword = Password });

        Assert.True(result.HasError);
        Assert.DoesNotContain("PasswordMismatch", result.Errors);
        Assert.Equal(0, _users.EmailChanges);
        Assert.Empty(_mailer.Sent);
    }
}
