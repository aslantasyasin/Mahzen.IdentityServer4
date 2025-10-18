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
        var claims = new List<Claim>
        {
            new Claim("full_name", user.FirstName + " " + user.LastName),
            new Claim("first_name", user.FirstName),
            new Claim("last_name", user.LastName),
            new Claim("email", user.Email),
            new Claim("user_name", user.UserName),
            new Claim("phone_number", user.PhoneNumber),
            new Claim("user_type", user.UserType.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
        };

        // sadece istenen scope’lara göre claim ekleme
        var requested = claims.Where(c => context.RequestedClaimTypes.Contains(c.Type));
        context.IssuedClaims.AddRange(requested);
        
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