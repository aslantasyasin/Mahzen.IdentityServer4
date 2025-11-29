using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer.Models;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Services.Profil;

public class CustomProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomProfileService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var user = await _userManager.GetUserAsync(context.Subject);
        if (user == null)
        {
            // no subject/user -> no claims
            return;
        }

        // helper: add claim only if it's requested and has a non-empty value
        void AddIfRequested(string type, string? value)
        {
            if (!context.RequestedClaimTypes.Contains(type)) return;
            if (string.IsNullOrWhiteSpace(value)) return;
            context.IssuedClaims.Add(new Claim(type, value));
        }

        // populate claims safely
        AddIfRequested("full_name", $"{user.FirstName ?? string.Empty} {user.LastName ?? string.Empty}".Trim());
        AddIfRequested("first_name", user.FirstName);
        AddIfRequested("last_name", user.LastName);
        AddIfRequested("email", user.Email);
        AddIfRequested("user_name", user.UserName);
        AddIfRequested("phone_number", user.PhoneNumber);
        AddIfRequested("user_type", user.UserType.ToString());
        AddIfRequested("tenant_id", user.TenantId.ToString());
        AddIfRequested("email_confirmed", user.EmailConfirmed.ToString());
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var user = await _userManager.GetUserAsync(context.Subject);

        // Kullanıcı mevcut mu?
        if (user == null)
        {
            context.IsActive = false;
            return;
        }

        // Hesap kilitli mi?
        if (await _userManager.IsLockedOutAsync(user))
        {
            context.IsActive = false;
            return;
        }

        // Her şey yolundaysa aktif
        context.IsActive = true;
    }
}