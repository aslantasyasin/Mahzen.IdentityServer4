using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Migrations.ApplicationDb;
using IdentityServer.Models;
using IdentityServer.Models.Base;
using IdentityServer.Models.Custom;
using IdentityServer.Models.Dto.Role;
using IdentityServer.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Services.Role
{
    public class RoleService : IRoleService
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IMapper _mapper;
        private readonly ICustomRepository _customRepository;
        private readonly UserInfo _userInfo;

        public RoleService(RoleManager<ApplicationRole> roleManager, IMapper mapper, ICustomRepository customRepository, IServiceScopeFactory serviceScopeFactory)
        {
            _roleManager = roleManager;
            _mapper = mapper;
            _customRepository = customRepository;
            _userInfo = new UserInfo(serviceScopeFactory);
        }

        public async Task<ApiResponse<bool>> CreateRoleAsync(RoleRequestDto createRole)
        {
            var response = new ApiResponse<bool>();

            try
            {
                var role = new ApplicationRole
                {
                    Id = createRole.Id,
                    Name = createRole.Name,
                    Description = createRole.Description,
                    TenantId = _userInfo.TenantId
                };

                var roleResult = await _roleManager.CreateAsync(role);

                response.Data = roleResult.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<RoleResponseDto>>> GetRolesAsync()
        {
            var response = new ApiResponse<List<RoleResponseDto>>();

            try
            {
                var roles = await _roleManager.Roles.ToListAsync();

                response.Data = _mapper.Map<List<ApplicationRole>, List<RoleResponseDto>>(roles);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> RemoveRoleAsync(string id)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                var roleResult = await _roleManager.DeleteAsync(role);

                response.Data = roleResult.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> UpdateRoleAsync(RoleRequestDto updateRole)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var role = await _roleManager.FindByIdAsync(updateRole.Id);
                role.Name = updateRole.Name;
                role.Description = updateRole.Description;

                var roleResult = await _roleManager.UpdateAsync(role);

                response.Data = roleResult.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> CreateRoleClaimAsync(RoleClaimRequestDto roleClaim)
        {
            var response = new ApiResponse<bool>();

            try
            {
                var role = await _roleManager.FindByIdAsync(roleClaim.RoleId);

                foreach (var item in roleClaim.Claims)
                {
                    Claim claim = new Claim(item.ClaimType, item.ClaimValue);

                    response.Data = (await _roleManager.AddClaimAsync(role, claim)).Succeeded;

                    if (!response.Data) break;
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> UpdateRoleClaimAsync(UpdateRoleClaimRequestDto updateRoleClaim)
        {
            var response = new ApiResponse<bool>();

            try
            {
                var role = await _roleManager.FindByIdAsync(updateRoleClaim.RoleId);

                foreach (var item in updateRoleClaim.OldClaims)
                {
                    Claim oldClaim = new Claim(item.ClaimType, item.ClaimValue);

                    await _roleManager.RemoveClaimAsync(role, oldClaim);
                }

                foreach (var item in updateRoleClaim.NewClaims)
                {
                    Claim newClaim = new Claim(item.ClaimType, item.ClaimValue);
                    response.Data = (await _roleManager.AddClaimAsync(role, newClaim)).Succeeded;
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> RemoveRoleClaimAsync(RoleClaimRequestDto roleClaim)
        {
            var response = new ApiResponse<bool>();

            try
            {
                var role = await _roleManager.FindByIdAsync(roleClaim.RoleId);

                foreach (var item in roleClaim.Claims)
                {
                    Claim claim = new Claim(item.ClaimType, item.ClaimValue);

                    response.Data = (await _roleManager.RemoveClaimAsync(role, claim)).Succeeded;

                    if (!response.Data) break;
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<RoleClaimResponseDto>>> GetRoleClaimsAsync(string roleId)
        {
            var response = new ApiResponse<List<RoleClaimResponseDto>>();

            try
            {
                var claims = await _roleManager.GetClaimsAsync(new ApplicationRole { Id = roleId });

                var claimGroup = claims.GroupBy(x => x.Value).ToList();

                response.Data = new List<RoleClaimResponseDto>();
                foreach (var claim in claimGroup)
                {
                    var roleClaims = new RoleClaimResponseDto();
                    roleClaims.Name = claim.Key;

                    foreach (var action in claim)
                        roleClaims.Actions.Add(action.Type);

                    response.Data.Add(roleClaims);
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<RoleClaimTypesResponseDto>>> GetRoleClaimTypesAsync()
        {
            var response = new ApiResponse<List<RoleClaimTypesResponseDto>>();

            try
            {
                var claimTypes = await _customRepository.GetAllViewTypes();

                response.Data = _mapper.Map<List<ViewType>, List<RoleClaimTypesResponseDto>>(claimTypes);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<RoleClaimValuesResponseDto>>> GetRoleClaimValuesAsync()
        {
            var response = new ApiResponse<List<RoleClaimValuesResponseDto>>();

            try
            {
                var claimValues = await _customRepository.GetAllViews();

                response.Data = _mapper.Map<List<View>, List<RoleClaimValuesResponseDto>>(claimValues);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }
    }
}

