using System.Collections.Concurrent;
using CoreApp.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CoreApp.Infrastructure.Services;

/// <summary>
/// In-memory cache implementation using IMemoryCache.
/// Tracks keys for prefix-based removal.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return cached;

        var value = await factory();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        };

        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);
        return value;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    public void RemoveByPrefix(string prefix)
    {
        var keysToRemove = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }
    }
}
