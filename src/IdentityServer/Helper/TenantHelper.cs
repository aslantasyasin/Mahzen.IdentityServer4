using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace IdentityServer.Helper
{
	public static class TenantHelper
	{
		public static int GetTenant(IServiceScopeFactory serviceScope)
		{
			using var scope = serviceScope.CreateScope();
			var httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            string authHeader = httpContext.HttpContext.Request.Headers["Authorization"];

			if (authHeader == null)
				return 0;
			else
			{
				var jwtToken = authHeader.Split(" ")[1];
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                JwtSecurityToken jwtSecurityToken = handler.ReadJwtToken(jwtToken);

                Claim tenantIdClaim = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tenantid");
                string tenantid = tenantIdClaim?.Value;

				return int.Parse(tenantid);
            }
		}
	}
}

