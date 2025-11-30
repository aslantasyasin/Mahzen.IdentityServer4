using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
    public class UpdateProfileInfoRequestDto
    {
        [MaxLength(100, ErrorMessage = "İsim en fazla 100 karakter olabilir.")]
        public string FirstName { get; set; }

        [MaxLength(100, ErrorMessage = "Soyisim en fazla 100 karakter olabilir.")]
        public string LastName { get; set; }

        [RegularExpression("^[1-9][0-9]{9}$", ErrorMessage = "Phone number must be exactly 10 digits and cannot start with 0.")]
        public string PhoneNumber { get; set; }
    }
}

