using System;
using IdentityServer.Models.Base;
using IdentityServer.Models.Dto.Role;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Models.Dto.User;
using IdentityServer.Models;

namespace IdentityServer.Services.User
{
	public interface IUserService
	{
        Task<ApiResponse<List<UserResponseDto>>> GetUsersAsync();
        Task<ApiResponse<bool>> CreateUserAsync(ApplicationUserRequestDto userRequestDto);
        Task<ApiResponse<bool>> UpdateUserAsync(string userId, ApplicationUserUpdateRequestDto userRequestDto);
        Task<ApiResponse<bool>> DeleteUserAsync(string userId);
        Task<ApiResponse<bool>> UserIsActiveControlAsync(string userName);
        Task<ApiResponse<List<ApplicationRole>>> GetRoleByUserIdAsync(string userId);
        Task<ApiResponse<bool>> AddUserRoleAsync(UserRoleRequestDto addUserRoleRequestDto);
        Task<ApiResponse<bool>> DeleteUserRoleAsync(UserRoleRequestDto addUserRoleRequestDto);
        Task<ApiResponse<List<UserMenuResponseDto>>> GetUserMenusAsync(string userId);
        Task<ApiResponse<UserResponseDto>> GetUserByIdAsync(string id);
	}
}

