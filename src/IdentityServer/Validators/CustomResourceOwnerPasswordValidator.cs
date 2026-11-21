using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer.Models;
using IdentityServer.Services.Login;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Validators;

public class CustomResourceOwnerPasswordValidator  : IResourceOwnerPasswordValidator
{
    public const string DeviceTokenParameter = "device_token";
    public const string StepUpIdParameter = "step_up_id";
    public const string StepUpCodeParameter = "step_up_code";
    public const string StepUpRequiredDescription = "step_up_required";
    public const string StepUpFailedDescription = "invalid_step_up_code";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LoginProtectionService _loginProtection;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomResourceOwnerPasswordValidator> _logger;

    public CustomResourceOwnerPasswordValidator(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        LoginProtectionService loginProtection,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<CustomResourceOwnerPasswordValidator> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _loginProtection = loginProtection;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        var user = await _userManager.FindByNameAsync(context.UserName)
                   ?? await _userManager.FindByEmailAsync(context.UserName);

        if (user == null)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Geçersiz kullanıcı adı veya parola");
            return;
        }

        // 2) Parola doğrula (bu adım ROPC için zorunlu)
        var source = LoginSource.Resolve(_httpContextAccessor.HttpContext, _configuration["Internal:ApiKey"]);
        var raw = context.Request?.Raw;
        var deviceId = await _loginProtection.FindTrustedDeviceAsync(user.Id, raw?[DeviceTokenParameter]);
        var passwordOk = await _signInManager.CheckPasswordSignInAsync(user, context.Password, lockoutOnFailure: !_loginProtection.Enforcing);
        if (passwordOk.IsLockedOut)
        {
            _logger.LogInformation("login_locked_out UserId={UserId} Source={Source}", user.Id, source);
        }
        if (!passwordOk.Succeeded)
        {
            await _loginProtection.RecordFailedPasswordAsync(user.Id, user.Email, source, deviceId, whileLockedOut: passwordOk.IsLockedOut);
        }

        if (!passwordOk.Succeeded)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Geçersiz kullanıcı adı veya parola");
            return;
        }

        // 3) Sadece client kontrolü (asıl ihtiyacınız)
        var clientId = context.Request?.Client?.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Client bilgisi eksik");
            return;
        }

        var claims = await _userManager.GetClaimsAsync(user);
        var userTypes = claims
            .Where(c => c.Type == "user_type") // ihtiyaca göre "allowed_client" gibi daha doğru bir tipe taşıyın
            .Select(c => c.Value)
            .ToHashSet();

        if (!IsClientAllowed(userTypes, context.Request.Client))
        {
            _logger.LogWarning("login_rejected_user_type UserId={UserId} ClientId={ClientId} UserTypes={UserTypes}",
                user.Id, clientId, string.Join(',', userTypes));
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Bu istemci için yetkiniz yok");
            return;
        }

        var outcome = await _loginProtection.CompleteAsync(new LoginAttempt(
            user.Id, user.Email, source, deviceId, userTypes.Contains("Admin"), raw?[StepUpIdParameter], raw?[StepUpCodeParameter]));

        context.Result = outcome.Kind switch
        {
            LoginOutcomeKind.StepUpRequired => new GrantValidationResult(TokenRequestErrors.InvalidGrant, StepUpRequiredDescription,
                new Dictionary<string, object> { [StepUpIdParameter] = outcome.ChallengeId!.Value.ToString() }),
            LoginOutcomeKind.StepUpFailed => new GrantValidationResult(TokenRequestErrors.InvalidGrant, StepUpFailedDescription),
            _ => new GrantValidationResult(subject: user.Id, authenticationMethod: "password",
                customResponse: outcome.DeviceToken is null ? null : new Dictionary<string, object> { [DeviceTokenParameter] = outcome.DeviceToken }),
        };
    }

    public const string AllowedUserTypesProperty = "allowed_user_types";

    public static bool IsClientAllowed(IReadOnlySet<string> userTypes, Client client)
    {
        if (userTypes.Contains("Admin") || userTypes.Contains(client.ClientId))
            return true;

        return client.Properties.TryGetValue(AllowedUserTypesProperty, out var allowed)
            && allowed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(userTypes.Contains);
    }
}