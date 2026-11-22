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
        public bool PasswordOk { get; set; } = true;

        public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(PasswordOk);

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
        var work = new LoginProtectionServiceTests.ImmediateWork();
        var protection = new LoginProtectionService(_store, _mail, work, new LoginFallbackLimiter(), Options.Create(new LoginProtectionOptions { Mode = mode }),
            NullLogger<LoginProtectionService>.Instance, new FixedClock());
        work.Service = protection;
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

    [Fact]
    public async Task Enforce_gives_an_admin_and_a_customer_with_a_wrong_password_the_same_answer()
    {
        var (validator, _) = Create(LoginProtectionMode.Enforce);
        _users.PasswordOk = false;

        _users.UserTypes = new[] { "B2C" };
        var customer = Context();
        await validator.ValidateAsync(customer);
        _users.UserTypes = new[] { "Admin" };
        var admin = Context();
        await validator.ValidateAsync(admin);

        Assert.True(customer.Result.IsError);
        Assert.Equal((customer.Result.Error, customer.Result.ErrorDescription, customer.Result.CustomResponse),
            (admin.Result.Error, admin.Result.ErrorDescription, admin.Result.CustomResponse));
    }

    [Fact]
    public async Task Shadow_checks_the_password_through_identity_with_lockout()
    {
        var (validator, signIn) = Create(LoginProtectionMode.Shadow);
        await validator.ValidateAsync(Context());

        Assert.True(signIn.LockoutOnFailure);
    }

    [Fact]
    public async Task Enforce_never_consults_the_identity_lockout()
    {
        var (validator, signIn) = Create(LoginProtectionMode.Enforce);
        signIn.Result = SignInResult.LockedOut;
        var context = Context();

        await validator.ValidateAsync(context);

        Assert.Null(signIn.LockoutOnFailure);
        Assert.False(context.Result.IsError);
    }

    [Fact]
    public async Task A_locked_out_attempt_in_shadow_is_rejected_and_counted()
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
    public async Task Under_the_gate_wrong_and_right_passwords_look_the_same_and_the_code_completes_the_login()
    {
        var (validator, _) = Create(LoginProtectionMode.Enforce);
        _users.PasswordOk = false;
        for (var i = 0; i < 5; i++) await validator.ValidateAsync(Context());

        var wrong = Context();
        await validator.ValidateAsync(wrong);
        _users.PasswordOk = true;
        var right = Context();
        await validator.ValidateAsync(right);

        Assert.Equal(CustomResourceOwnerPasswordValidator.StepUpRequiredDescription, right.Result.ErrorDescription);
        Assert.Equal(wrong.Result.ErrorDescription, right.Result.ErrorDescription);
        Assert.Equal(wrong.Result.CustomResponse[CustomResourceOwnerPasswordValidator.StepUpIdParameter],
            right.Result.CustomResponse[CustomResourceOwnerPasswordValidator.StepUpIdParameter]);

        var verify = Context((CustomResourceOwnerPasswordValidator.StepUpIdParameter, (string)right.Result.CustomResponse[CustomResourceOwnerPasswordValidator.StepUpIdParameter]),
            (CustomResourceOwnerPasswordValidator.StepUpCodeParameter, _mail.LastCode));
        await validator.ValidateAsync(verify);
        Assert.False(verify.Result.IsError);
    }

    [Fact]
    public async Task Under_the_gate_a_rejected_user_type_gets_the_same_answer_as_a_wrong_password()
    {
        var (validator, _) = Create(LoginProtectionMode.Enforce);
        _users.PasswordOk = false;
        for (var i = 0; i < 6; i++) await validator.ValidateAsync(Context());
        var wrong = Context();
        await validator.ValidateAsync(wrong);

        _users.PasswordOk = true;
        _users.UserTypes = new[] { "Default" };
        var rejectedType = Context();
        await validator.ValidateAsync(rejectedType);

        Assert.Equal(wrong.Result.ErrorDescription, rejectedType.Result.ErrorDescription);
        Assert.Equal(wrong.Result.CustomResponse[CustomResourceOwnerPasswordValidator.StepUpIdParameter],
            rejectedType.Result.CustomResponse[CustomResourceOwnerPasswordValidator.StepUpIdParameter]);
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task A_rejected_user_type_looks_like_a_wrong_password_and_never_triggers_a_code_or_a_device()
    {
        _users.UserTypes = new[] { "Default" };
        var (validator, _) = Create(LoginProtectionMode.Enforce);
        var context = Context();

        await validator.ValidateAsync(context);

        Assert.Equal(CustomResourceOwnerPasswordValidator.InvalidCredentialsDescription, context.Result.ErrorDescription);
        Assert.Empty(_mail.Sent);
        Assert.Empty(_store.Devices);
    }
}
