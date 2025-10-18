using IdentityServer.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models;

public class ApplicationRole : IdentityRole, ITenant
{
    public string Description { get; set; }
    public int TenantId { get ; set ; }
}