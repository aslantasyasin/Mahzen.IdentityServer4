using System.Threading.Tasks;
using IdentityServer.Models;

namespace IdentityServer.Services.User
{
    public interface IUserChangeLogService
    {
        Task LogChangeAsync(string userId, string fieldName, string oldValue, string newValue, string changedBy);
    }
}

