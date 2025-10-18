using System;
using IdentityServer.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
	public class ApplicationUserRequestDto
	{
        public string Id { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        
        public string Password { get; set; }
        public bool IsActive { get; set; }
        public UserType UserType { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public int TenantId { get; set; }
    }
}

