using System;
using System.Collections.Generic;

namespace IdentityServer.Models.Dto.Role
{
	public class RoleClaimResponseDto
	{
		public RoleClaimResponseDto()
		{
			Actions = new List<string>();
        }
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string Name { get; set; }
		public List<string> Actions { get; set; }

    }
}

