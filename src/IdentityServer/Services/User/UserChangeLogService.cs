using System;
using System.Threading.Tasks;
using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Services.User
{
    public class UserChangeLogService : IUserChangeLogService
    {
        private readonly CustomDbContext _customDbContext;
        private readonly UserInfo _userInfo;

        public UserChangeLogService(CustomDbContext customDbContext, IServiceScopeFactory serviceScopeFactory)
        {
            _customDbContext = customDbContext;
            _userInfo = new UserInfo(serviceScopeFactory);
        }

        public async Task LogChangeAsync(string userId, string fieldName, string oldValue, string newValue, string changedBy)
        {
            if (oldValue == newValue)
                return;

            var log = new UserChangeLog
            {
                UserId = userId,
                FieldName = fieldName,
                OldValue = oldValue ?? string.Empty,
                NewValue = newValue ?? string.Empty,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
                TenantId = _userInfo.TenantId
            };

            await _customDbContext.UserChangeLogs.AddAsync(log);
            await _customDbContext.SaveChangesAsync();
        }
    }
}

