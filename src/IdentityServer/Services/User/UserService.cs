using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Models;
using IdentityServer.Models.Base;
using IdentityServer.Models.Dto.Role;
using IdentityServer.Models.Dto.User;
using IdentityServer.Models.Enums;
using IdentityServer.Repositories;
using IdentityServer.Repositories.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUlid;

namespace IdentityServer.Services.User
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserInfo _userInfo;
        private readonly IMapper _mapper;
        private readonly IIdentityRepository _identityRepository;
        private readonly ICustomRepository _customRepository;

        public UserService(UserManager<ApplicationUser> userManager, IServiceScopeFactory serviceScopeFactory, IMapper mapper, IIdentityRepository identityRepository, ICustomRepository customRepository)
        {
            _userManager = userManager;
            _userInfo = new UserInfo(serviceScopeFactory);
            _mapper = mapper;
            _identityRepository = identityRepository;
            _customRepository = customRepository;
        }

        public async Task<ApiResponse<bool>> CreateUserAsync(ApplicationUserRequestDto userRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                userRequestDto.Id = Ulid.NewUlid().ToString();
                userRequestDto.UserName = userRequestDto.Email;
                var map = _mapper.Map<ApplicationUserRequestDto, ApplicationUser>(userRequestDto);
                var addUserResult = await _userManager.CreateAsync(map, userRequestDto.Password);
                if (addUserResult.Succeeded)
                {
                    var claims = new List<Claim>();
                    var tenantClaim = new Claim("tenantid", _userInfo.TenantId.ToString());
                    var userNameClaim = new Claim("username", map.UserName);

                    claims.Add(tenantClaim);
                    claims.Add(userNameClaim);

                    var claimresult = await _userManager.AddClaimsAsync(map, claims);
                    response.Data = addUserResult.Succeeded;
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> DeleteUserAsync(string userId)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                var deleteUserResult = await _userManager.DeleteAsync(user);

                response.Data = deleteUserResult.Succeeded;

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<UserResponseDto>>> GetUsersAsync()
        {
            var response = new ApiResponse<List<UserResponseDto>>();
            try
            {
                var users = await _userManager.Users.Where(x => x.TenantId == _userInfo.TenantId).ToListAsync();

                response.Data = _mapper.Map<List<ApplicationUser>, List<UserResponseDto>>(users);

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> UserIsActiveControlAsync(string userName)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByNameAsync(userName);

                if (user == null)
                    response.Errors.Add("Böyle bir kullanıcı bulunamadı!");
                else if (!user.IsActive)
                    response.Errors.Add("Kullanıcı akif değil! Yöneticinize başvurun.");
                else
                    response.Data = true;
                
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> UpdateUserAsync(string userId, ApplicationUserUpdateRequestDto userRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                UserMap(userRequestDto, user);
                var updateUserResult = await _userManager.UpdateAsync(user);

                response.Data = updateUserResult.Succeeded;

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<ApplicationRole>>> GetRoleByUserIdAsync(string userId)
        {
            var response = new ApiResponse<List<ApplicationRole>>();
            try
            {
                response.Data = await _identityRepository.GetRolesByUserId(userId);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> AddUserRoleAsync(UserRoleRequestDto addUserRoleRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(addUserRoleRequestDto.UserId);
                var result = await _userManager.AddToRoleAsync(user, addUserRoleRequestDto.RoleName);

                response.Data = result.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> DeleteUserRoleAsync(UserRoleRequestDto addUserRoleRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(addUserRoleRequestDto.UserId);
                var result = await _userManager.RemoveFromRoleAsync(user, addUserRoleRequestDto.RoleName);

                response.Data = result.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<UserMenuResponseDto>>> GetUserMenusAsync(string userId)
        {
            var response = new ApiResponse<List<UserMenuResponseDto>>();
            try
            {
                var roleClaims = await _identityRepository.GetUserRoleClaims(userId);

                var views = await _customRepository.GetAllViews();

                var userMenus = new List<UserMenuResponseDto>();
                
                foreach (var main in views.Where(x=> x.IsMainMenu).ToList())
                {
                    var mainMenu = new UserMenuResponseDto();

                    mainMenu.MenuName = main.Name;
                    mainMenu.MenuTrName = main.TrName;
                    mainMenu.IconName = main.IconName;
                    mainMenu.Path = main.Path;

                    mainMenu.SubMenus = new List<UserSubMenuDto>();

                    foreach (var sub in views.Where(x=> x.UpMenuId == main.Id).ToList())
                    {
                        if(roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.View.ToString()))
                        {
                            var subMenu = new UserSubMenuDto();

                            subMenu.MenuName = sub.Name;
                            subMenu.MenuTrName = sub.TrName;
                            subMenu.IconName = sub.IconName;
                            subMenu.Path = sub.Path;

                            subMenu.IsView = true;
                            subMenu.IsCreate = roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.Create.ToString());
                            subMenu.IsUpdate = roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.Update.ToString());
                            subMenu.IsDelete = roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.Delete.ToString());

                            subMenu.ParentViews = new List<UserParentView>();

                            foreach (var parent in views.Where(x => x.ParentId == sub.Id).ToList())
                            {
                                var parentView = new UserParentView();

                                parentView.MenuName = parent.Name;
                                parentView.MenuTrName = parent.TrName;

                                parentView.IsView = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.View.ToString());
                                parentView.IsCreate = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.Create.ToString());
                                parentView.IsUpdate = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.Update.ToString());
                                parentView.IsDelete = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.Delete.ToString());

                                subMenu.ParentViews.Add(parentView);
                            }
                            mainMenu.SubMenus.Add(subMenu);
                        }
                        
                    }
                    if(mainMenu.SubMenus.Any() || !string.IsNullOrEmpty(mainMenu.Path))
                        userMenus.Add(mainMenu);

                }
                response.Data = userMenus;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        private void UserMap(ApplicationUserUpdateRequestDto userRequestDto, ApplicationUser user)
        {
            if (userRequestDto.IsActive != null)
                user.IsActive = userRequestDto.IsActive ?? false;
            if (userRequestDto.UserName != null)
                user.UserName = userRequestDto.UserName;
            if (userRequestDto.FirstName != null)
                user.FirstName = userRequestDto.FirstName;
            if (userRequestDto.LastName != null)
                user.LastName = userRequestDto.LastName;
            if (userRequestDto.UserType != null)
                user.UserType = userRequestDto.UserType ?? UserType.Default;
            if (userRequestDto.Email != null)
                user.Email = userRequestDto.Email;
        }
    }
}

