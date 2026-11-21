using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using IdentityServer.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services.Login;

public sealed class LoginThrottleStore : ILoginThrottleStore
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

    private readonly CustomDbContext _db;

    public LoginThrottleStore(CustomDbContext db) => _db = db;

    public async Task<int> IncrementAsync(string userId, string scope, DateTimeOffset windowStart) =>
        Convert.ToInt32(await ScalarAsync(IncrementSql, userId, scope, windowStart));

    public async Task<int> CountAsync(string userId, string scope, DateTimeOffset windowStart) =>
        await ScalarAsync(CountSql, userId, scope, windowStart) is { } value and not DBNull ? Convert.ToInt32(value) : 0;

    private async Task<object> ScalarAsync(string sql, string userId, string scope, DateTimeOffset windowStart)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "userId", userId);
        AddParameter(command, "scope", scope);
        AddParameter(command, "windowStart", windowStart.UtcDateTime);
        return await command.ExecuteScalarAsync();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
