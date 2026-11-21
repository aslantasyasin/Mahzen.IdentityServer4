using System;
using System.Threading.Tasks;

namespace IdentityServer.Services.Login;

public interface ILoginProtectionStore
{
    Task<int> IncrementAsync(string userId, string scope, DateTimeOffset windowStart);

    Task<int> CountAsync(string userId, string scope, DateTimeOffset windowStart);

    Task<long?> FindActiveDeviceAsync(string userId, string tokenHash, DateTimeOffset now);

    Task AddDeviceAsync(string userId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt);

    Task TouchDeviceAsync(long deviceId, DateTimeOffset now, DateTimeOffset expiresAt);

    Task RevokeDeviceAsync(long deviceId, DateTimeOffset now, string reason);

    Task<LoginChallenge> FindOpenChallengeAsync(string userId, DateTimeOffset now);

    Task<LoginChallenge> GetChallengeAsync(Guid id);

    Task CreateChallengeAsync(Guid id, string userId, string codeHash, int maxAttempts, DateTimeOffset now, DateTimeOffset expiresAt);

    Task<bool> TryRotateChallengeCodeAsync(Guid id, string codeHash, DateTimeOffset now, DateTimeOffset sentBefore);

    Task<bool> TryConsumeChallengeAttemptAsync(Guid id, DateTimeOffset now);

    Task<bool> TryCompleteChallengeAsync(Guid id, DateTimeOffset now);

    Task<bool> TryClaimNoticeAsync(string userId, string kind, DateTimeOffset now, DateTimeOffset sentBefore);

    Task ResetUserAsync(string userId, DateTimeOffset now, string reason);

    Task<int> CleanupAsync(DateTimeOffset now, DateTimeOffset throttleBefore, int batchSize);
}
