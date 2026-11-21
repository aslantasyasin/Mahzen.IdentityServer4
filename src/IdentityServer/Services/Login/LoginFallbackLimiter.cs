using System;
using Microsoft.Extensions.Caching.Memory;

namespace IdentityServer.Services.Login;

public sealed class LoginFallbackLimiter : IDisposable
{
    public const int MaxEntries = 100_000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });
    private readonly object _gate = new();

    public void RecordFailure(string userId, string source, TimeSpan window)
    {
        var key = Key(userId, source);
        lock (_gate)
        {
            var count = _cache.TryGetValue(key, out int current) ? current + 1 : 1;
            _cache.Set(key, count, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = window });
        }
    }

    public bool IsBlocked(string userId, string source, int limit)
    {
        lock (_gate) return _cache.TryGetValue(Key(userId, source), out int count) && count >= limit;
    }

    public void Dispose() => _cache.Dispose();

    private static string Key(string userId, string source) => userId + "|" + source;
}
