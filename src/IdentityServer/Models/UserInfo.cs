using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Models
{
	public class UserInfo
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		public UserInfo(IServiceScopeFactory serviceScope)
		{
			using var scope = serviceScope.CreateScope();
			_httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		}

		public int TenantId => int.TryParse(User?.FindFirst("tenantid")?.Value, out var tenantId) ? tenantId : 0;

		public string UserId => User?.FindFirst("sub")?.Value;

		private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User;
	}
}
