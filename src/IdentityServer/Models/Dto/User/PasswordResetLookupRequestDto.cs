using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
    public class PasswordResetLookupRequestDto
    {
        [Required(ErrorMessage = "Email adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; }
    }
}
