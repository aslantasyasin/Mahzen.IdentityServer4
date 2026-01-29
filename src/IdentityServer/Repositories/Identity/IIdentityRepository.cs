using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Models;
using IdentityServer.Models.Dto.User;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Repositories.Identity
{
	public interface IIdentityRepository
	{
        Task<List<ApplicationRole>> GetRolesByUserId(string userId);
        Task<List<IdentityRoleClaim<string>>> GetUserRoleClaims(string userId);
        Task<List<UserResponseDto>> GetUsersAsync();
	}
}

