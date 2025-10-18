using System;
using IdentityServer.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models
{
	public class ApplicationRoleClaim
        //: IdentityRoleClaim<string>, ITenant
	{
        public int TenantId { get; set ; }
    }
}

