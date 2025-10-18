using System;
using IdentityServer.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models
{
	public class ApplicationUserClaim
		//: IdentityUserClaim<int>, ITenant
    {
		public int TenantId { get; set; }
	}
}

