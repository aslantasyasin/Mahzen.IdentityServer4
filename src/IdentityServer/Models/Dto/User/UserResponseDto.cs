using System;
namespace IdentityServer.Models.Dto.User
{
	public class UserResponseDto
	{
		public string Id { get; set; }
		public string UserName { get; set; }
		public string FirstName { get; set; }
		public string FullName { get; set; }
		public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool Status { get; set; }
        public string Role { get; set; }
	}
}

