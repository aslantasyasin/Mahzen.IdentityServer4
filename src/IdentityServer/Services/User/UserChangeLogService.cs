using System;
using System.Collections.Generic;
using System.Linq;
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

        public Task LogChangeAsync(string userId, string fieldName, string oldValue, string newValue, string changedBy) =>
            LogChangesAsync(userId, new[] { (fieldName, oldValue, newValue) }, changedBy);

        public async Task LogChangesAsync(string userId, IReadOnlyList<(string FieldName, string OldValue, string NewValue)> changes, string changedBy)
        {
            var now = DateTime.UtcNow;
            var logs = changes.Select(change => new UserChangeLog
            {
                UserId = userId,
                FieldName = change.FieldName,
                OldValue = change.OldValue ?? string.Empty,
                NewValue = change.NewValue ?? string.Empty,
                ChangedAt = now,
                ChangedBy = changedBy,
                TenantId = _userInfo.TenantId
            });

            await _customDbContext.UserChangeLogs.AddRangeAsync(logs);
            await _customDbContext.SaveChangesAsync();
        }
    }
}
