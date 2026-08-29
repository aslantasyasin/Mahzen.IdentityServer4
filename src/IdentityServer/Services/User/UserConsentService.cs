using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.Dto.User;
using IdentityServer.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace IdentityServer.Services.User
{
    public class UserConsentService : IUserConsentService
    {
        // UserAgent kolonunun sınırı. Tarayıcılar uzun UA gönderebilir; kırpmak,
        // ispat değeri düşük bir alan yüzünden onay kaydını tamamen kaybetmekten iyidir.
        private const int UserAgentMaxLength = 512;

        private readonly CustomDbContext _customDbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserConsentService(CustomDbContext customDbContext, IHttpContextAccessor httpContextAccessor)
        {
            _customDbContext = customDbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task RecordAsync(string userId, int tenantId, IReadOnlyList<ConsentAcceptanceDto> documents, string source)
        {
            if (documents == null || documents.Count == 0)
                throw new ArgumentException("Onay listesi boş olamaz.", nameof(documents));

            // Tek zaman damgası: aynı istekte kabul edilen dokümanlar aynı anı taşımalı,
            // aksi halde kayıtlar arasında anlamsız milisaniye farkları oluşur.
            var acceptedAt = DateTime.UtcNow;
            var ipAddress = ReadClientIp();
            var userAgent = Truncate(ReadHeader("X-Client-User-Agent"), UserAgentMaxLength);

            foreach (var document in documents)
            {
                // Tür doğrulaması çağıranda da yapılıyor; burada savunma amaçlı tekrar
                // ediliyor: geçersiz bir tür sessizce yazılırsa kayıt ispat değerini yitirir.
                // IsDefined şart — TryParse tek başına "999" gibi tanımsız sayısal değerleri
                // de başarıyla ayrıştırır ve enum'a aralık dışı bir değer yazar.
                if (!Enum.TryParse<ConsentDocumentType>(document.DocumentType, out var documentType)
                    || !Enum.IsDefined(typeof(ConsentDocumentType), documentType))
                {
                    throw new ArgumentException($"Bilinmeyen doküman türü: {document.DocumentType}", nameof(documents));
                }

                await _customDbContext.UserConsent.AddAsync(new UserConsent
                {
                    UserId = userId,
                    TenantId = tenantId,
                    DocumentType = documentType,
                    DocumentVersion = document.Version,
                    ContentHash = document.ContentHash,
                    Source = source,
                    AcceptedAt = acceptedAt,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                });
            }

            await _customDbContext.SaveChangesAsync();
        }

        // IP ve User-Agent, BFF tarafından sunucu tarafında set edilen header'larla gelir
        // (mahzen.ui/src/app/api/auth/register/route.js). Doğrudan RemoteIpAddress okunmaz:
        // istek gateway üzerinden geldiği için orada gateway'in IP'si görünür.
        // BİLİNEN SINIR: Register endpoint'i AllowAnonymous ve gateway üzerinden dışarı açık;
        // doğrudan IDS4'e istek atan biri bu header'ı uydurabilir. Gateway'de istemciden
        // gelen X-Client-* header'larının strip edilmesi ayrı bir iş kalemidir.
        private string ReadHeader(string name)
        {
            var values = _httpContextAccessor.HttpContext?.Request?.Headers[name] ?? StringValues.Empty;
            // İlk değer alınıyor: header birden fazla kez gönderilirse StringValues.ToString()
            // hepsini virgülle birleştirir ve denetim tablosuna birleşik bir çöp değer yazılır.
            var value = values.Count > 0 ? values[0] : null;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        // IP biçimi doğrulanır ve normalize edilir. NEDEN: uç AllowAnonymous olduğu için
        // header doğrudan IDS4'e istek atan biri tarafından uydurulabilir; en azından IP
        // biçiminde olmayan serbest metin hukuki denetim tablosuna yazılmasın. Doğrulanmış
        // IP her zaman 45 karakterin altında kaldığı için ayrıca kırpma gerekmez.
        private string ReadClientIp()
        {
            var raw = ReadHeader("X-Client-Ip");
            if (raw == null) return null;

            // X-Forwarded-For tarzı zincir gelirse ilk (istemciye en yakın) değer alınır.
            var first = raw.Split(',')[0].Trim();
            return IPAddress.TryParse(first, out var parsed) ? parsed.ToString() : null;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
