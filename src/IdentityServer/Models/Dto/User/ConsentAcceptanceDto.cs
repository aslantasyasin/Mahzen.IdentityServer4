using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
    // Kayıt isteğiyle birlikte gelen tek bir doküman onayı.
    // DocumentType neden string: bu uygulamada JsonStringEnumConverter global olarak
    // kayıtlı değil; enum'a bind edebilmek için JSON ayarını değiştirmek gerekirdi ve
    // bu tüm endpoint'lerin davranışını etkilerdi. Servis katmanında Enum.TryParse ile
    // çözülür, bilinmeyen değer reddedilir.
    public class ConsentAcceptanceDto
    {
        [Required(ErrorMessage = "Document type is required.")]
        [MaxLength(64)]
        public string DocumentType { get; set; }

        [Required(ErrorMessage = "Document version is required.")]
        [MaxLength(32)]
        public string Version { get; set; }

        [Required(ErrorMessage = "Document content hash is required.")]
        [MaxLength(64)]
        public string ContentHash { get; set; }
    }
}
