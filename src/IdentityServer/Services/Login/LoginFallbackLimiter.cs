using System;
using Microsoft.Extensions.Caching.Memory;

namespace IdentityServer.Services.Login;

public sealed class LoginFallbackLimiter : IDisposable
{
    public const int MaxEntries = 100_000;

    private sealed record Entry(int Count, DateTimeOffset ExpiresAt);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });
    private readonly object _gate = new();

    public int RecordAttempt(string userId, string source, TimeSpan window, DateTimeOffset now)
    {
        var key = userId + "|" + source;
        lock (_gate)
        {
            var entry = _cache.TryGetValue(key, out Entry current) && current.ExpiresAt > now
                ? current with { Count = current.Count + 1 }
                : new Entry(1, now + window);
            _cache.Set(key, entry, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = entry.ExpiresAt - now });
            return _cache.TryGetValue(key, out Entry _) ? entry.Count : int.MaxValue;
        }
    }

    public void Dispose() => _cache.Dispose();
}
