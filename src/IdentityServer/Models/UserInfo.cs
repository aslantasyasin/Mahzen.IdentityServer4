﻿using System;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace IdentityServer.Models
{
	public class UserInfo
	{
		private readonly IServiceScopeFactory _serviceScope;

        public UserInfo(IServiceScopeFactory serviceScope)
		{
			_serviceScope = serviceScope;
            PropSetInfo();
		}

		public int TenantId { get; set; }
		public string UserId { get; set; }

		private void PropSetInfo()
		{
            using var scope = _serviceScope.CreateScope();
            var httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            string authHeader = httpContext.HttpContext.Request.Headers["Authorization"];

            if (authHeader != null)
            {
                var jwtToken = authHeader.Split(" ")[1];
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                JwtSecurityToken jwtSecurityToken = handler.ReadJwtToken(jwtToken);

                Claim tenantIdClaim = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "tenantid");
                if(tenantIdClaim != null)
                {
                    string tenantid = tenantIdClaim?.Value;
                    this.TenantId = int.Parse(tenantid);
                }
                
                Claim userIdClaim = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "sub");
                if(userIdClaim != null)
                {
                    this.UserId = userIdClaim?.Value;
                }
            }
        }
	}
}

