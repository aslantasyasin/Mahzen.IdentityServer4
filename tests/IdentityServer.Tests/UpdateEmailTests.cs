using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Models;
using IdentityServer.Models.Dto.User;
using IdentityServer.Services.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.Tests;

public class UpdateEmailTests
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

        public Users() : base(new NoStore(), null!, null!, null!, null!, null!, null!, null!, null!) { }

        public override Task<ApplicationUser> FindByIdAsync(string userId) => Task.FromResult(userId == UserId ? Customer : null!);
        public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(password == Password);
        public override Task<ApplicationUser> FindByEmailAsync(string email) =>
            Task.FromResult(email == TakenEmail ? new ApplicationUser { Id = "user-2", Email = TakenEmail } : null!);
        public override Task<string> GenerateChangeEmailTokenAsync(ApplicationUser user, string newEmail) => Task.FromResult("token");
        public override Task<IdentityResult> ChangeEmailAsync(ApplicationUser user, string newEmail, string token)
        {
            EmailChanges++;
            user.Email = newEmail;
            return Task.FromResult(IdentityResult.Success);
        }
        public override Task<IdentityResult> UpdateAsync(ApplicationUser user) => Task.FromResult(IdentityResult.Success);
    }

    private sealed class NoChangeLog : IUserChangeLogService
    {
        public Task LogChangeAsync(string userId, string fieldName, string oldValue, string newValue, string changedBy) => Task.CompletedTask;
    }

    private readonly Users _users = new();

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
            NullLogger<UserService>.Instance, null!, null!);
    }

    [Fact]
    public async Task The_right_password_changes_the_email()
    {
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
    }
}
