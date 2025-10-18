using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Models.Base;
using IdentityServer.Models.Dto.Role;

namespace IdentityServer.Services.Role
{
	public interface IRoleService
    {
		Task<ApiResponse<bool>> CreateRoleAsync(RoleRequestDto createRole);
        Task<ApiResponse<List<RoleResponseDto>>> GetRolesAsync();
        Task<ApiResponse<bool>> UpdateRoleAsync(RoleRequestDto updateRole);
        Task<ApiResponse<bool>> RemoveRoleAsync(string id);

        Task<ApiResponse<bool>> CreateRoleClaimAsync(RoleClaimRequestDto roleClaim);
        Task<ApiResponse<bool>> UpdateRoleClaimAsync(UpdateRoleClaimRequestDto updateRoleClaim);
        Task<ApiResponse<bool>> RemoveRoleClaimAsync(RoleClaimRequestDto roleClaim);
        Task<ApiResponse<List<RoleClaimResponseDto>>> GetRoleClaimsAsync(string roleId);
        Task<ApiResponse<List<RoleClaimTypesResponseDto>>> GetRoleClaimTypesAsync();
        Task<ApiResponse<List<RoleClaimValuesResponseDto>>> GetRoleClaimValuesAsync();
    }
}


