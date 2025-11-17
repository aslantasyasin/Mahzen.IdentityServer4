using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
    public class ChangePasswordRequestDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(20)]
        public string NewPassword { get; set; }

        [Required]
        public string CurrentPassword { get; set; }
    }
}
