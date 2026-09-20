namespace IdentityServer.Models.Dto.User
{
    // Şifre sıfırlama isteğinin ilk adımı: e-postanın arkasında mail gönderilebilir
    // bir kullanıcı var mı?
    //
    // Bu sonuç SON KULLANICIYA ASLA DÖNMEZ. Kayıtlı/kayıtsız ayrımını istemciye
    // sızdırmak, siteyi e-posta sayım (enumeration) aracına çevirirdi. Yalnızca
    // Notification servisi okur ve tek bir karar için kullanır: mail atılsın mı.
    // Notification da kendi yanıtını girdiden bağımsız olarak hep aynı üretir.
    public class PasswordResetLookupResponseDto
    {
        // Durum kodları SERVİSLER ARASI SÖZLEŞMEDİR. Karşı taraf (Notification)
        // bunları metin olarak karşılaştırıyor; enum kullanılmadı çünkü varsayılan
        // JSON serileştirmesi enum'u sayıya çevirir ve iki serviste ayrı ayrı
        // tanımlanan sayılar sessizce kayabilir.
        public const string StatusOk = "ok";
        public const string StatusNotFound = "not_found";
        public const string StatusInactive = "inactive";

        public string Status { get; set; }

        // Yalnızca Status == StatusOk iken doludur.
        public string UserId { get; set; }
    }
}
