using System.Security.Claims;
using IdentityModel;
using IdentityServer.Models;
using IdentityServer.Services.Login;
using IdentityServer.Services.Profil;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityServer.Tests;

public class ProfileIsActiveTests
{
    private static readonly DateTimeOffset ChangedAt = new(2026, 11, 22, 15, 41, 41, 140, TimeSpan.Zero);

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
        public ApplicationUser User = new() { Id = "user-1", UserName = "ali@ornek.com", IsActive = true };
        public bool LockedOut;

        public Users() : base(new NoStore(), null!, null!, null!, null!, null!, null!, null!, null!) { }

        public override Task<ApplicationUser> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(User);
        public override Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(LockedOut);
        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) => Task.FromResult<IList<string>>(new List<string>());
    }

    private sealed class FixedClock : Microsoft.AspNetCore.Authentication.ISystemClock
    {
        public DateTimeOffset UtcNow => ChangedAt;
    }

    private static CustomProfileService Service(Users users, LoginProtectionMode mode = LoginProtectionMode.Shadow)
    {
        var work = new LoginProtectionServiceTests.ImmediateWork();
        var protection = new LoginProtectionService(new LoginProtectionServiceTests.MemoryStore(), new LoginProtectionServiceTests.RecordingMailer(), work,
            new LoginFallbackLimiter(), Options.Create(new LoginProtectionOptions { Mode = mode }), NullLogger<LoginProtectionService>.Instance, new FixedClock());
        work.Service = protection;
        return new CustomProfileService(users, protection);
    }

    private static async Task<bool> IsActive(Users users, long? authTime, string? passwordVersion = null, LoginProtectionMode mode = LoginProtectionMode.Shadow)
    {
        var claims = new List<Claim> { new(JwtClaimTypes.Subject, "user-1") };
        if (authTime is { } t) claims.Add(new Claim(JwtClaimTypes.AuthenticationTime, t.ToString()));
        if (passwordVersion is not null) claims.Add(new Claim(CustomProfileService.PasswordVersionClaim, passwordVersion));
        var context = new IsActiveContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), new Client(), "test");
        await Service(users, mode).IsActiveAsync(context);
        return context.IsActive;
    }

    [Fact]
    public async Task A_user_who_never_changed_the_password_stays_active() =>
        Assert.True(await IsActive(new Users(), authTime: null));

    [Fact]
    public async Task A_session_from_before_the_password_change_is_not_active()
    {
        var users = new Users { User = { PasswordChangedAt = ChangedAt } };

        Assert.False(await IsActive(users, ChangedAt.ToUnixTimeSeconds() - 1));
    }

    [Fact]
    public async Task A_session_from_the_same_second_as_the_change_is_active()
    {
        var users = new Users { User = { PasswordChangedAt = ChangedAt } };

        Assert.True(await IsActive(users, ChangedAt.ToUnixTimeSeconds()));
    }

    [Fact]
    public async Task A_session_from_after_the_change_is_active()
    {
        var users = new Users { User = { PasswordChangedAt = ChangedAt } };

        Assert.True(await IsActive(users, ChangedAt.ToUnixTimeSeconds() + 60));
    }

    [Fact]
    public async Task A_session_without_an_authentication_time_is_not_active_once_the_password_changed()
    {
        var users = new Users { User = { PasswordChangedAt = ChangedAt } };

        Assert.False(await IsActive(users, authTime: null));
    }

    [Fact]
    public async Task A_locked_out_user_is_not_active() =>
        Assert.False(await IsActive(new Users { LockedOut = true }, authTime: null));

    [Fact]
    public async Task Enforce_ignores_the_identity_lockout_so_the_token_and_the_session_agree() =>
        Assert.True(await IsActive(new Users { LockedOut = true }, authTime: null, mode: LoginProtectionMode.Enforce));

    [Fact]
    public async Task An_inactive_user_is_not_active() =>
        Assert.False(await IsActive(new Users { User = { IsActive = false } }, authTime: null));

    [Fact]
    public async Task A_session_that_checked_the_old_password_is_not_active_even_if_issued_after_the_change()
    {
        var users = new Users { User = { PasswordChangedAt = ChangedAt } };

        Assert.False(await IsActive(users, ChangedAt.ToUnixTimeSeconds() + 1, passwordVersion: "0"));
    }

    [Fact]
    public async Task A_session_that_checked_the_current_password_is_active()
    {
        var users = new Users { User = { PasswordChangedAt = ChangedAt } };

        Assert.True(await IsActive(users, ChangedAt.ToUnixTimeSeconds() + 1, CustomProfileService.PasswordVersion(users.User)));
    }

    [Theory]
    [InlineData(IdentityServer4.IdentityServerConstants.ProfileDataCallers.ClaimsProviderAccessToken, true)]
    [InlineData(IdentityServer4.IdentityServerConstants.ProfileDataCallers.UserInfoEndpoint, false)]
    public async Task Only_the_access_token_carries_the_password_version(string caller, bool expected)
    {
        var subject = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtClaimTypes.Subject, "user-1"),
            new Claim(CustomProfileService.PasswordVersionClaim, "42"),
        }, "test"));
        var context = new ProfileDataRequestContext(subject, new Client(), caller, new string[0]);

        await Service(new Users()).GetProfileDataAsync(context);

        Assert.Equal(expected, context.IssuedClaims.Any(c => c.Type == CustomProfileService.PasswordVersionClaim && c.Value == "42"));
    }
}
