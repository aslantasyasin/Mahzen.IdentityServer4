using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using IdentityServer.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services.Login;

public sealed class LoginProtectionStore : ILoginProtectionStore
{
    private const string IncrementSql = @"
INSERT INTO ids4.""LoginThrottle"" (""UserId"", ""Scope"", ""WindowStart"", ""Failures"")
VALUES (@userId, @scope, @windowStart, 1)
ON CONFLICT (""UserId"", ""Scope"", ""WindowStart"")
DO UPDATE SET ""Failures"" = ids4.""LoginThrottle"".""Failures"" + 1
RETURNING ""Failures"";";

    private const string CountSql = @"
SELECT ""Failures"" FROM ids4.""LoginThrottle""
WHERE ""UserId"" = @userId AND ""Scope"" = @scope AND ""WindowStart"" = @windowStart;";

    private const string FindDeviceSql = @"
SELECT ""Id"" FROM ids4.""LoginDevice""
WHERE ""TokenHash"" = @tokenHash AND ""UserId"" = @userId AND ""RevokedAt"" IS NULL AND ""ExpiresAt"" > @now;";

    private const string AddDeviceSql = @"
INSERT INTO ids4.""LoginDevice"" (""UserId"", ""TokenHash"", ""CreatedAt"", ""LastUsedAt"", ""ExpiresAt"")
VALUES (@userId, @tokenHash, @now, @now, @expiresAt);";

    private const string TouchDeviceSql = @"
UPDATE ids4.""LoginDevice"" SET ""LastUsedAt"" = @now, ""ExpiresAt"" = @expiresAt
WHERE ""Id"" = @id AND ""RevokedAt"" IS NULL;";

    private const string RevokeDeviceSql = @"
UPDATE ids4.""LoginDevice"" SET ""RevokedAt"" = @now, ""RevokeReason"" = @reason
WHERE ""Id"" = @id AND ""RevokedAt"" IS NULL;";

    private const string ChallengeColumns = @"""Id"", ""UserId"", ""CodeHash"", ""ExpiresAt"", ""LastSentAt"", ""UsedAt"" IS NOT NULL";

    private const string FindOpenChallengeSql = @"
SELECT " + ChallengeColumns + @" FROM ids4.""LoginChallenge""
WHERE ""UserId"" = @userId AND ""UsedAt"" IS NULL AND ""ExpiresAt"" > @now AND ""Attempts"" < ""MaxAttempts""
ORDER BY ""CreatedAt"" DESC LIMIT 1;";

    private const string GetChallengeSql = @"
SELECT " + ChallengeColumns + @" FROM ids4.""LoginChallenge"" WHERE ""Id"" = @id;";

    private const string CreateChallengeSql = @"
INSERT INTO ids4.""LoginChallenge"" (""Id"", ""UserId"", ""CodeHash"", ""Attempts"", ""MaxAttempts"", ""CreatedAt"", ""ExpiresAt"", ""LastSentAt"")
VALUES (@id, @userId, @codeHash, 0, @maxAttempts, @now, @expiresAt, @now);";

    private const string RotateChallengeSql = @"
UPDATE ids4.""LoginChallenge"" SET ""CodeHash"" = @codeHash, ""LastSentAt"" = @now
WHERE ""Id"" = @id AND ""UsedAt"" IS NULL AND ""ExpiresAt"" > @now AND ""LastSentAt"" <= @sentBefore;";

    private const string ConsumeAttemptSql = @"
UPDATE ids4.""LoginChallenge"" SET ""Attempts"" = ""Attempts"" + 1
WHERE ""Id"" = @id AND ""UsedAt"" IS NULL AND ""ExpiresAt"" > @now AND ""Attempts"" < ""MaxAttempts"";";

    private const string CompleteChallengeSql = @"
UPDATE ids4.""LoginChallenge"" SET ""UsedAt"" = @now WHERE ""Id"" = @id AND ""UsedAt"" IS NULL;";

    private const string ClaimNoticeSql = @"
INSERT INTO ids4.""LoginNotice"" (""UserId"", ""Kind"", ""SentAt"") VALUES (@userId, @kind, @now)
ON CONFLICT (""UserId"", ""Kind"") DO UPDATE SET ""SentAt"" = @now
WHERE ids4.""LoginNotice"".""SentAt"" <= @sentBefore
RETURNING 1;";

    private const string ResetUserSql = @"
DELETE FROM ids4.""LoginThrottle"" WHERE ""UserId"" = @userId;
UPDATE ids4.""LoginDevice"" SET ""RevokedAt"" = @now, ""RevokeReason"" = @reason WHERE ""UserId"" = @userId AND ""RevokedAt"" IS NULL;
UPDATE ids4.""LoginChallenge"" SET ""UsedAt"" = @now WHERE ""UserId"" = @userId AND ""UsedAt"" IS NULL;";

    private static readonly string[] CleanupSql =
    {
        @"DELETE FROM ids4.""LoginThrottle"" WHERE ctid IN (SELECT ctid FROM ids4.""LoginThrottle"" WHERE ""WindowStart"" < @throttleBefore LIMIT @batch);",
        @"DELETE FROM ids4.""LoginChallenge"" WHERE ctid IN (SELECT ctid FROM ids4.""LoginChallenge"" WHERE ""ExpiresAt"" < @now LIMIT @batch);",
        @"DELETE FROM ids4.""LoginDevice"" WHERE ctid IN (SELECT ctid FROM ids4.""LoginDevice"" WHERE ""ExpiresAt"" < @now OR ""RevokedAt"" < @now LIMIT @batch);",
    };

    private readonly CustomDbContext _db;

    public LoginProtectionStore(CustomDbContext db) => _db = db;

    public async Task<int> IncrementAsync(string userId, string scope, DateTimeOffset windowStart) =>
        Convert.ToInt32(await ScalarAsync(IncrementSql, ("userId", userId), ("scope", scope), ("windowStart", Utc(windowStart))));

    public async Task<int> CountAsync(string userId, string scope, DateTimeOffset windowStart) =>
        await ScalarAsync(CountSql, ("userId", userId), ("scope", scope), ("windowStart", Utc(windowStart))) is { } value and not DBNull
            ? Convert.ToInt32(value)
            : 0;

    public async Task<long?> FindActiveDeviceAsync(string userId, string tokenHash, DateTimeOffset now) =>
        await ScalarAsync(FindDeviceSql, ("userId", userId), ("tokenHash", tokenHash), ("now", Utc(now))) is { } value and not DBNull
            ? Convert.ToInt64(value)
            : null;

    public Task AddDeviceAsync(string userId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt) =>
        ExecuteAsync(AddDeviceSql, ("userId", userId), ("tokenHash", tokenHash), ("now", Utc(now)), ("expiresAt", Utc(expiresAt)));

    public Task TouchDeviceAsync(long deviceId, DateTimeOffset now, DateTimeOffset expiresAt) =>
        ExecuteAsync(TouchDeviceSql, ("id", deviceId), ("now", Utc(now)), ("expiresAt", Utc(expiresAt)));

    public Task RevokeDeviceAsync(long deviceId, DateTimeOffset now, string reason) =>
        ExecuteAsync(RevokeDeviceSql, ("id", deviceId), ("now", Utc(now)), ("reason", reason));

    public Task<LoginChallenge> FindOpenChallengeAsync(string userId, DateTimeOffset now) =>
        ReadChallengeAsync(FindOpenChallengeSql, ("userId", userId), ("now", Utc(now)));

    public Task<LoginChallenge> GetChallengeAsync(Guid id) =>
        ReadChallengeAsync(GetChallengeSql, ("id", id));

    public Task CreateChallengeAsync(Guid id, string userId, string codeHash, int maxAttempts, DateTimeOffset now, DateTimeOffset expiresAt) =>
        ExecuteAsync(CreateChallengeSql, ("id", id), ("userId", userId), ("codeHash", codeHash), ("maxAttempts", maxAttempts),
            ("now", Utc(now)), ("expiresAt", Utc(expiresAt)));

    public async Task<bool> TryRotateChallengeCodeAsync(Guid id, string codeHash, DateTimeOffset now, DateTimeOffset sentBefore) =>
        await ExecuteAsync(RotateChallengeSql, ("id", id), ("codeHash", codeHash), ("now", Utc(now)), ("sentBefore", Utc(sentBefore))) == 1;

    public async Task<bool> TryConsumeChallengeAttemptAsync(Guid id, DateTimeOffset now) =>
        await ExecuteAsync(ConsumeAttemptSql, ("id", id), ("now", Utc(now))) == 1;

    public async Task<bool> TryCompleteChallengeAsync(Guid id, DateTimeOffset now) =>
        await ExecuteAsync(CompleteChallengeSql, ("id", id), ("now", Utc(now))) == 1;

    public async Task<bool> TryClaimNoticeAsync(string userId, string kind, DateTimeOffset now, DateTimeOffset sentBefore) =>
        await ScalarAsync(ClaimNoticeSql, ("userId", userId), ("kind", kind), ("now", Utc(now)), ("sentBefore", Utc(sentBefore))) is { } value and not DBNull;

    public async Task ResetUserAsync(string userId, DateTimeOffset now, string reason)
    {
        var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = Command(connection, ResetUserSql, new (string, object)[] { ("userId", userId), ("now", Utc(now)), ("reason", reason) });
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    public async Task<int> CleanupAsync(DateTimeOffset now, DateTimeOffset throttleBefore, int batchSize)
    {
        var deleted = 0;
        foreach (var sql in CleanupSql)
        {
            int affected;
            do
            {
                affected = await ExecuteAsync(sql, ("now", Utc(now)), ("throttleBefore", Utc(throttleBefore)), ("batch", batchSize));
                deleted += affected;
            } while (affected == batchSize);
        }
        return deleted;
    }

    private async Task<LoginChallenge> ReadChallengeAsync(string sql, params (string Name, object Value)[] parameters)
    {
        var connection = await OpenAsync();
        await using var command = Command(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new LoginChallenge(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)),
            reader.GetBoolean(5));
    }

    private async Task<object> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        var connection = await OpenAsync();
        await using var command = Command(connection, sql, parameters);
        return await command.ExecuteScalarAsync();
    }

    private async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        var connection = await OpenAsync();
        await using var command = Command(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<DbConnection> OpenAsync()
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        return connection;
    }

    private static DbCommand Command(DbConnection connection, string sql, IEnumerable<(string Name, object Value)> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static DateTime Utc(DateTimeOffset value) => value.UtcDateTime;
}
