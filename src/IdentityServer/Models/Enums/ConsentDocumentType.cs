namespace IdentityServer.Models.Enums;

// Onaya tabi hukuki doküman türleri. Değerler DB'ye metin olarak yazılır
// (CustomDbContext -> HasConversion<string>), bu yüzden isimler değiştirilemez:
// değiştirilirse eski onay kayıtları eşleşmez.
public enum ConsentDocumentType
{
    TermsOfUse = 1,
    PrivacyPolicy = 2
}
