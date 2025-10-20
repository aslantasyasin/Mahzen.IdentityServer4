using IdentityServer.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
	public class ApplicationUserRequestDto
	{
        public string Id { get; set; }
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; }
        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; }
        
        [Required(ErrorMessage = "Password is required.")]
        [StringLength(20, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 20 characters long.")]
        public string Password { get; set; }
        public bool IsActive { get; set; }
        public UserType UserType { get; set; }
        
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }
        
       
        public string UserName { get; set; }
        
        public int TenantId { get; set; }
        
        [Required(ErrorMessage = "Country code is required.")]       
        [RegularExpression(@"^\d{1,3}$", ErrorMessage = "Country code must be 1-3 digits without '+' (e.g. 90 or 1).")]
        public string CountryCode { get; set; } ="90";
        
        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression("^[1-9][0-9]{9}$", ErrorMessage = "Phone number must be exactly 10 digits and cannot start with 0.")]
        public string PhoneNumber { get; set; }
    }
}
