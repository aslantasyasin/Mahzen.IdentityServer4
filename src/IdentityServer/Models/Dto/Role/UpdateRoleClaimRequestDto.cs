using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IdentityServer.Models.Dto.Claim;

namespace IdentityServer.Models.Dto.Role
{
	public class UpdateRoleClaimRequestDto
	{
        public List<ClaimRequestDto> NewClaims { get; set; }
        public List<ClaimRequestDto> OldClaims { get; set; }
        public string RoleId { get; set; }
    }
}

