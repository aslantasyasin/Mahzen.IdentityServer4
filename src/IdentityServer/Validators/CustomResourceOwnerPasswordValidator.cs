using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer.Models;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Validators;

public class CustomResourceOwnerPasswordValidator  : IResourceOwnerPasswordValidator
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomResourceOwnerPasswordValidator(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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
        var passwordOk = await _signInManager.CheckPasswordSignInAsync(user, context.Password, lockoutOnFailure: true);
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
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Bu istemci için yetkiniz yok");
            return;
        }

        // 4) Başarılı
        context.Result = new GrantValidationResult(subject: user.Id, authenticationMethod: "password");
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