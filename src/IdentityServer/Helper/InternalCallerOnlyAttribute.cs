using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Helper
{
    // [AllowAnonymous] işaretli ama yalnızca kendi BFF'imiz tarafından çağrılması gereken
    // uçları paylaşımlı bir anahtarla korur.
    //
    // NEDEN gateway'de değil burada: IDS4 dış dünyaya doğrudan açık (OIDC discovery ve
    // token uçları için açık olmak zorunda). Gateway'e konan bir kontrol, gateway atlanıp
    // doğrudan IDS4'e istek atılarak aşılırdı.
    //
    // NEDEN paylaşımlı anahtar: bu uçların çağıranı son kullanıcı değil BFF'in kendisi;
    // ortada doğrulanabilecek bir kullanıcı token'ı yok. IP tabanlı bir beyaz liste ise
    // K8S'te pod IP'leri değiştiği için kırılgan olurdu.
    //
    // NEDEN anahtar tanımlı değilken geçiriyor: deploy sırası (önce IDS4, sonra BFF)
    // kayıt akışını kesmemeli. Bu bilinçli bir "fail-open"; her istekte Warning düşer,
    // yapılandırma unutulursa log'dan görülür.
    //
    // AMA bu davranış her uç için kabul edilebilir DEĞİL. Şifre sıfırlama uçları
    // (LookupForPasswordReset, ResetPasswordByService) bu filtrenin arkasına
    // konduğunda denklem değişti: anahtarsız bir IDS4, yalnızca e-posta bilen
    // birinin herhangi bir hesabın şifresini değiştirebilmesi demek. Önce lookup
    // ile userId alınır, sonra reset çağrılır; koda hiç ihtiyaç yoktur.
    //
    // Required = true o uçlar için fail-CLOSED yapar: anahtar yoksa istek reddedilir.
    // Geçiş riski yok, anahtar üç serviste de tanımlı.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class InternalCallerOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public const string HeaderName = "X-Internal-Key";
        private const string ConfigurationKey = "Internal:ApiKey";

        // Anahtar yapılandırılmamışken ucun ne yapacağı. Varsayılan (false)
        // mevcut davranışı korur; hesap devralmaya açık uçlarda true verilir.
        public bool Required { get; init; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var services = context.HttpContext.RequestServices;
            var expectedKey = services.GetRequiredService<IConfiguration>()[ConfigurationKey];
            var logger = services.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(InternalCallerOnlyAttribute));
            var path = context.HttpContext.Request.Path.Value;

            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                if (Required)
                {
                    // 503, 403 DEĞİL: sorun çağıranın kimliğinde değil, sunucunun
                    // yapılandırmasında. Ayrım, operasyonun logda doğru yeri
                    // aramasını sağlıyor.
                    logger.LogError(
                        "Internal:ApiKey yapılandırılmamış; {Path} kapatıldı. Anahtar tanımlanmadan bu uç açılamaz.",
                        path);
                    context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                    return;
                }

                logger.LogWarning(
                    "Internal:ApiKey yapılandırılmamış; {Path} kimliksiz çağrılara açık. Anahtar tanımlanmalı.",
                    path);
                return;
            }

            var providedValues = context.HttpContext.Request.Headers[HeaderName];
            var providedKey = providedValues.Count > 0 ? providedValues[0] : null;

            if (KeysMatch(expectedKey, providedKey)) return;

            // Anahtarın kendisi loglanmaz. Yanıt gövdesi de neden reddedildiğini söylemez:
            // deneyen tarafa "böyle bir başlık var" bilgisi verilmez (md. 9).
            logger.LogWarning("Geçersiz {Header} ile çağrı reddedildi. Path={Path}", HeaderName, path);
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }

        // Sabit zamanlı karşılaştırma: uzunluk dışında bilgi sızdırmaz, byte byte
        // deneyerek anahtar çıkarmayı engeller.
        private static bool KeysMatch(string expected, string provided)
        {
            if (string.IsNullOrEmpty(provided)) return false;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided));
        }
    }
}
