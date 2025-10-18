using System;
using IdentityServer.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models
{
	public class ApplicationUserRole
		//: IdentityUserRole<string>, ITenant
	{
		public int TenantId { get; set; }
	}
}

