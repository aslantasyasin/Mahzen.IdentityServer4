using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer.Models.Dto.Role;
using IdentityServer.Services.Role;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IdentityServer4.IdentityServerConstants;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace IdentityServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(LocalApi.PolicyName)]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(RoleRequestDto createRoleRequestDto)
        {
            var result = await _roleService.CreateRoleAsync(createRoleRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);
            
            return Ok(result.Data);
        }

        [HttpPost("CreateClaim")]
        public async Task<IActionResult> CreateClaim(RoleClaimRequestDto createRoleClaimRequestDto)
        {
            var result = await _roleService.CreateRoleClaimAsync(createRoleClaimRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPost("UpdateClaim")]
        public async Task<IActionResult> UpdateClaim(UpdateRoleClaimRequestDto updateRoleClaimRequestDto)
        {
            var result = await _roleService.UpdateRoleClaimAsync(updateRoleClaimRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPost("RemoveClaim")]
        public async Task<IActionResult> RemoveClaim(RoleClaimRequestDto createRoleClaimRequestDto)
        {
            var result = await _roleService.RemoveRoleClaimAsync(createRoleClaimRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("GetRoles")]
        public async Task<IActionResult> GetRoles()
        {
            var result = await _roleService.GetRolesAsync();

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("GetRoleClaimsById")]
        public async Task<IActionResult> GetRoleClaimsById(string roleId)
        {
            var result = await _roleService.GetRoleClaimsAsync(roleId);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update(RoleRequestDto updateRoleRequestDto)
        {
            var result = await _roleService.UpdateRoleAsync(updateRoleRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _roleService.RemoveRoleAsync(id);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("GetClaimTypes")]
        public async Task<IActionResult> GetClaimTypes()
        {
            var result = await _roleService.GetRoleClaimTypesAsync();

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("GetClaimValues")]
        public async Task<IActionResult> GetClaimValues()
        {
            var result = await _roleService.GetRoleClaimValuesAsync();

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

    }
}

