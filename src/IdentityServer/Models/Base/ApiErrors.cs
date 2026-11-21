namespace IdentityServer.Models.Base
{
    public static class ApiErrors
    {
        // Beklenmeyen hatalarda istemciye dönen genel mesaj. NEDEN ex.Message dönülmüyor:
        // IdS uçları gateway üzerinden dışarı açık ve web 4xx gövdesini tarayıcıya iletiyor;
        // Postgres/Identity hata metinleri şema, kolon ve altyapı bilgisi sızdırır (md. 9).
        // Ayrıntı log'a yazılır, istemciye gitmez.
        public const string Unexpected = "İşlem tamamlanamadı, lütfen tekrar deneyin.";
    }
}
