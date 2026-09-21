namespace IdentityServer.Helper
{
    // Loglara yazılan kişisel verinin maskelenmesi.
    //
    // Notification servisindeki aynı adlı yardımcının eşi. İki kopya olması
    // servis sınırından kaynaklanıyor; ortak bir pakete çıkarmak bu iki
    // metot için orantısız olurdu.
    //
    // NEDEN: şifre sıfırlama akışının yanıtları bilinçli olarak bilgi
    // taşımıyor, bu yüzden loglar tek kayıt. Ama tam adresi yazmak PII'yi
    // log altyapısına taşımak demek (md. 9). Alan adı ve ilk/son harf,
    // operasyonun kaydı ayırt etmesine yetiyor.
    public static class LogMaskHelper
    {
        private const string Empty = "(boş)";
        private const string Hidden = "***";

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return Empty;

            var at = email.IndexOf('@');

            // "@" yoksa adres değildir; belirsiz bir metni kısmen göstermektense
            // tamamen gizle.
            if (at <= 0 || at == email.Length - 1) return Hidden;

            var local = email.Substring(0, at);
            var domain = email.Substring(at + 1);

            // Çok kısa yerel kısımda ilk/son harfi göstermek adresin tamamını
            // ele verir.
            if (local.Length <= 3) return $"{Hidden}@{domain}";

            return $"{local[0]}{Hidden}{local[local.Length - 1]}@{domain}";
        }
    }
}
