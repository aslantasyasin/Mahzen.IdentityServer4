using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
    // Mevcut şifreyi İSTEMEZ: bu akışın kullanıcısı şifresini zaten bilmiyor.
    // Kimlik kanıtı, kullanıcının posta kutusuna gönderilen kodun geri girilmesiyle
    // Notification tarafında üretiliyor. Bu yüzden uç [InternalCallerOnly] olmak
    // zorunda — dışarıdan çağrılabilseydi "userId ver, şifreyi değiştir" demek olurdu.
    public class ResetPasswordByServiceRequestDto
    {
        [Required(ErrorMessage = "Kullanıcı bilgisi zorunludur.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Yeni şifre zorunludur.")]
        public string NewPassword { get; set; }
    }
}
