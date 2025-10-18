using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IdentityServer.Models.Dto.Claim;

namespace IdentityServer.Models.Dto.Role
{
	public class RoleClaimRequestDto
	{
		public List<ClaimRequestDto> Claims { get; set; }
		public string RoleId { get; set; }
	}
}

