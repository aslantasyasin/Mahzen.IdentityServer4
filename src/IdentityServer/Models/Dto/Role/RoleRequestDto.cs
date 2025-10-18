using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.Role
{
	public class RoleRequestDto
	{
        public string Id { get; set; }
        [Required]
		public string Name { get; set; }
		public string Description { get; set; }
	}
}

