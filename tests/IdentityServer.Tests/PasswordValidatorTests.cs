using System.Collections.Specialized;
using System.Security.Claims;
using IdentityServer.Models;
using IdentityServer.Services.Login;
using IdentityServer.Validators;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityServer.Tests;

public class PasswordValidatorTests
{
    private static readonly ApplicationUser Customer = new() { Id = "user-1", UserName = "ali@ornek.com", Email = "ali@ornek.com" };

    private sealed class StubUserStore : IUserStore<ApplicationUser>
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

    private sealed class FakeUserManager : UserManager<ApplicationUser>
    {
        public string[] UserTypes { get; set; } = { "B2C" };

        public FakeUserManager() : base(new StubUserStore(), null!, null!, null!, null!, null!, null!, null!, null!) { }

        public override Task<ApplicationUser> FindByNameAsync(string userName) => Task.FromResult(userName == Customer.UserName ? Customer : null!);
        public override Task<ApplicationUser> FindByEmailAsync(string email) => Task.FromResult<ApplicationUser>(null!);
        public override Task<IList<Claim>> GetClaimsAsync(ApplicationUser user) =>
            Task.FromResult<IList<Claim>>(UserTypes.Select(t => new Claim("user_type", t)).ToList());
    }

    private sealed class StubPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user) => Task.FromResult(new ClaimsPrincipal());
    }

    private sealed class FakeSignInManager : SignInManager<ApplicationUser>
    {
        public SignInResult Result { get; set; } = SignInResult.Success;
        public bool? LockoutOnFailure { get; private set; }

        public FakeSignInManager(UserManager<ApplicationUser> users)
            : base(users, new HttpContextAccessor(), new StubPrincipalFactory(), null!, NullLogger<SignInManager<ApplicationUser>>.Instance, null!, null!) { }

        public override Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
        {
            LockoutOnFailure = lockoutOnFailure;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly LoginProtectionServiceTests.MemoryStore _store = new();
    private readonly LoginProtectionServiceTests.RecordingMailer _mail = new();
    private readonly FakeUserManager _users = new();

    private (CustomResourceOwnerPasswordValidator Validator, FakeSignInManager SignIn) Create(LoginProtectionMode mode)
    {
        var signIn = new FakeSignInManager(_users);
        var protection = new LoginProtectionService(_store, _mail, Options.Create(new LoginProtectionOptions { Mode = mode }),
            NullLogger<LoginProtectionService>.Instance, new FixedClock());
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.7") } } };
        var validator = new CustomResourceOwnerPasswordValidator(signIn, _users, protection, http,
            new ConfigurationBuilder().Build(), NullLogger<CustomResourceOwnerPasswordValidator>.Instance);
        return (validator, signIn);
    }

    private static ResourceOwnerPasswordValidationContext Context(params (string Key, string Value)[] extra)
    {
        var raw = new NameValueCollection();
        foreach (var (key, value) in extra) raw[key] = value;
        return new ResourceOwnerPasswordValidationContext
        {
            UserName = Customer.UserName,
            Password = "secret",
            Request = new ValidatedTokenRequest
            {
                Raw = raw,
                Client = new Client
                {
                    ClientId = "mahzen-mobile-bff",
                    Properties = new Dictionary<string, string> { [CustomResourceOwnerPasswordValidator.AllowedUserTypesProperty] = "B2C" },
                },
            },
        };
    }

    [Theory]
    [InlineData(LoginProtectionMode.Shadow, true)]
    [InlineData(LoginProtectionMode.Enforce, false)]
    public async Task Identity_lockout_is_only_turned_off_when_enforcing(LoginProtectionMode mode, bool lockout)
    {
        var (validator, signIn) = Create(mode);
        await validator.ValidateAsync(Context());

        Assert.Equal(lockout, signIn.LockoutOnFailure);
    }

    [Fact]
    public async Task A_locked_out_attempt_is_rejected_and_counted()
    {
        var (validator, signIn) = Create(LoginProtectionMode.Shadow);
        signIn.Result = SignInResult.LockedOut;
        var context = Context();

        await validator.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains(_store.Counts.Keys, k => k.Scope == LoginProtectionService.AccountUntrustedScope);
    }

    [Fact]
    public async Task A_successful_login_returns_a_device_token()
    {
        var (validator, _) = Create(LoginProtectionMode.Enforce);
        var context = Context();

        await validator.ValidateAsync(context);

        Assert.False(context.Result.IsError);
        Assert.Equal("user-1", context.Result.Subject.FindFirst("sub")?.Value);
        Assert.IsType<string>(context.Result.CustomResponse[CustomResourceOwnerPasswordValidator.DeviceTokenParameter]);
    }

    [Fact]
    public async Task A_known_device_token_is_recognised_and_not_reissued()
    {
        var (validator, _) = Create(LoginProtectionMode.Enforce);
        var first = Context();
        await validator.ValidateAsync(first);
        var token = (string)first.Result.CustomResponse[CustomResourceOwnerPasswordValidator.DeviceTokenParameter];

        var second = Context((CustomResourceOwnerPasswordValidator.DeviceTokenParameter, token));
        await validator.ValidateAsync(second);

        Assert.False(second.Result.IsError);
        Assert.Null(second.Result.CustomResponse);
        Assert.Single(_store.Devices);
    }

    [Fact]
    public async Task Step_up_is_returned_as_invalid_grant_with_a_challenge_id_and_completes_with_the_code()
    {
        var (validator, signIn) = Create(LoginProtectionMode.Enforce);
        signIn.Result = SignInResult.Failed;
        for (var i = 0; i < 5; i++) await validator.ValidateAsync(Context());
        signIn.Result = SignInResult.Success;

        var context = Context();
        await validator.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Equal("invalid_grant", context.Result.Error);
        Assert.Equal(CustomResourceOwnerPasswordValidator.StepUpRequiredDescription, context.Result.ErrorDescription);
        var challengeId = (string)context.Result.CustomResponse[CustomResourceOwnerPasswordValidator.StepUpIdParameter];

        var verify = Context((CustomResourceOwnerPasswordValidator.StepUpIdParameter, challengeId),
            (CustomResourceOwnerPasswordValidator.StepUpCodeParameter, _mail.LastCode));
        await validator.ValidateAsync(verify);
        Assert.False(verify.Result.IsError);
    }

    [Fact]
    public async Task A_rejected_user_type_never_triggers_a_code_or_a_device()
    {
        _users.UserTypes = new[] { "Default" };
        var (validator, signIn) = Create(LoginProtectionMode.Enforce);
        signIn.Result = SignInResult.Failed;
        for (var i = 0; i < 5; i++) await validator.ValidateAsync(Context());
        signIn.Result = SignInResult.Success;

        var context = Context();
        await validator.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Null(context.Result.CustomResponse);
        Assert.Empty(_mail.Sent);
        Assert.Empty(_store.Devices);
    }
}
