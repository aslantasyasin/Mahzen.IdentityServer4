using System;
using IdentityServer.Models.Base;
using IdentityServer.Models.Enums;

namespace IdentityServer.Models
{
    // Kullanıcının bir hukuki dokümanı kabul ettiğine dair ispat kaydı.
    // Append-only: yazıldıktan sonra güncellenmez, kullanıcı silinse bile durur.
    public class UserConsent : ITenant
    {
        // Kaynak sabitleri: onayın hangi akışta toplandığı. Bugün tek değer var,
        // ileride sipariş/pazarlama onayları aynı tabloya yazılabilsin diye kolon
        // baştan duruyor (append-only tabloya sonradan NOT NULL kolon eklemek
        // backfill gerektirir).
        public const string SourceRegister = "Register";

        public int Id { get; set; }
        public string UserId { get; set; }
        public int TenantId { get; set; }
        public ConsentDocumentType DocumentType { get; set; }
        public string DocumentVersion { get; set; }
        // Kabul edilen metnin SHA-256 özeti. Sürüm etiketi tek başına ispat değildir;
        // metin sessizce değişirse sürüm aynı kalabilir. Özet, arşivdeki metnin
        // kabul edilen metin olduğunu kanıtlar.
        public string ContentHash { get; set; }
        public string Source { get; set; }
        public DateTime AcceptedAt { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }
}
