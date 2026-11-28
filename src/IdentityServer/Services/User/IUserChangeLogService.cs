using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Models;

namespace IdentityServer.Services.User
{
    public interface IUserChangeLogService
    {
        Task LogChangeAsync(string userId, string fieldName, string oldValue, string newValue, string changedBy);
        Task LogChangesAsync(string userId, IReadOnlyList<(string FieldName, string OldValue, string NewValue)> changes, string changedBy);
    }
}

