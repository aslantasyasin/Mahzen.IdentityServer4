using System;
using System.Threading.Tasks;

namespace IdentityServer.Services.Login;

public interface ILoginThrottleStore
{
    Task<int> IncrementAsync(string userId, string scope, DateTimeOffset windowStart);

    Task<int> CountAsync(string userId, string scope, DateTimeOffset windowStart);
}
