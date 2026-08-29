using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Models.Dto.User;

namespace IdentityServer.Services.User
{
    public interface IUserConsentService
    {
        // Verilen dokümanların tamamını tek SaveChanges ile yazar. Hata durumunda
        // exception fırlatır: çağıran (UserService) telafi edici geri alma yapar.
        Task RecordAsync(string userId, int tenantId, IReadOnlyList<ConsentAcceptanceDto> documents, string source);
    }
}
