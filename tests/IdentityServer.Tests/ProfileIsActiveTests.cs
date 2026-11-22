using System.Security.Claims;
using IdentityModel;
using IdentityServer.Models;
using IdentityServer.Services.Profil;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Identity;

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
        public ApplicationUser User = new() { Id = "user-1", UserName = "ali@ornek.com" };
        public bool LockedOut;

        public Users() : base(new NoStore(), null!, null!, null!, null!, null!, null!, null!, null!) { }

        public override Task<ApplicationUser> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(User);
        public override Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(LockedOut);
    }

    private static async Task<bool> IsActive(Users users, long? authTime)
    {
        var claims = new List<Claim> { new(JwtClaimTypes.Subject, "user-1") };
        if (authTime is { } t) claims.Add(new Claim(JwtClaimTypes.AuthenticationTime, t.ToString()));
        var context = new IsActiveContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), new Client(), "test");
        await new CustomProfileService(users).IsActiveAsync(context);
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
}
