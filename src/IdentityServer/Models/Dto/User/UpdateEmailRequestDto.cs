using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
    public class UpdateEmailRequestDto
    {
        [Required(ErrorMessage = "Email adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
        public string CurrentPassword { get; set; }
    }
}

