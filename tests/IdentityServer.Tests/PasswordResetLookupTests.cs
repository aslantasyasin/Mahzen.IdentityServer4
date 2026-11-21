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

public class PasswordResetLookupTests
{
    private static readonly ApplicationUser Customer = new() { Id = "user-1", UserName = "ali@ornek.com", Email = "ali@ornek.com", IsActive = true };
    private const string OwnToken = "kendi-cihazi";
    private const string OtherUsersToken = "baskasinin-cihazi";

    private sealed class EmptyUserStore : IUserStore<ApplicationUser>
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

    private sealed class SingleUserManager : UserManager<ApplicationUser>
    {
        public SingleUserManager() : base(new EmptyUserStore(), null!, null!, null!, null!, null!, null!, null!, null!) { }

        public override Task<ApplicationUser> FindByEmailAsync(string email) =>
            Task.FromResult(email == Customer.Email ? Customer : null!);
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly LoginProtectionServiceTests.MemoryStore _store = new();

    private UserService Service(LoginProtectionMode mode = LoginProtectionMode.Enforce)
    {
        var work = new LoginProtectionServiceTests.ImmediateWork();
        var protection = new LoginProtectionService(_store, new LoginProtectionServiceTests.RecordingMailer(), work, new LoginFallbackLimiter(),
            Options.Create(new LoginProtectionOptions { Mode = mode }), NullLogger<LoginProtectionService>.Instance, new FixedClock());
        work.Service = protection;
        var scopes = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() })
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        return new UserService(new SingleUserManager(), null!, scopes, null!, null!, null!, null!, null!,
            NullLogger<UserService>.Instance, null!, protection, null!);
    }

    private async Task AddDevice(string userId, string token)
    {
        var now = new FixedClock().UtcNow;
        await _store.AddDeviceAsync(userId, LoginSecrets.HashDeviceToken(token), now, now.AddDays(90));
    }

    [Fact]
    public async Task The_accounts_own_device_is_reported_as_trusted()
    {
        await AddDevice(Customer.Id, OwnToken);

        var result = await Service().LookupForPasswordResetAsync(Customer.Email, OwnToken);

        Assert.Equal(PasswordResetLookupResponseDto.StatusOk, result.Data.Status);
        Assert.True(result.Data.DeviceTrusted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("taninmayan")]
    [InlineData(OtherUsersToken)]
    public async Task A_missing_unknown_or_foreign_device_is_not_trusted(string? token)
    {
        await AddDevice("user-2", OtherUsersToken);

        var result = await Service().LookupForPasswordResetAsync(Customer.Email, token!);

        Assert.Equal(PasswordResetLookupResponseDto.StatusOk, result.Data.Status);
        Assert.False(result.Data.DeviceTrusted);
    }

    [Fact]
    public async Task No_device_is_trusted_while_protection_is_off()
    {
        await AddDevice(Customer.Id, OwnToken);

        var result = await Service(LoginProtectionMode.Off).LookupForPasswordResetAsync(Customer.Email, OwnToken);

        Assert.False(result.Data.DeviceTrusted);
    }
}
