namespace CoreApp.Application.Interfaces;

/// <summary>
/// Simple cache abstraction. Backed by IMemoryCache in Infrastructure.
/// Use for frequently-read, rarely-changed data (roles, permissions, settings).
/// </summary>
public interface ICacheService
{
    /// <summary>Gets a cached item, or creates and caches it using the factory.</summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    /// <summary>Removes a specific key from the cache.</summary>
    void Remove(string key);

    /// <summary>Removes all keys matching a prefix (e.g., "Roles:" clears all role caches).</summary>
    void RemoveByPrefix(string prefix);
}

/// <summary>Well-known cache key prefixes.</summary>
public static class CacheKeys
{
    public const string Roles = "Roles:";
    public const string Permissions = "Permissions:";
    public const string Users = "Users:";
    public const string Settings = "Settings:";

    public static string RoleById(int id) => $"{Roles}{id}";
    public static string PermissionsByRole(int roleId) => $"{Permissions}Role:{roleId}";
    public static string UserById(int id) => $"{Users}{id}";
}
