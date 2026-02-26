using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Core
{
    public class ServiceCache
    {
        private class CacheEntry
        {
            public object Value { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        public async Task<T> GetOrLoadAsync<T>(string key, Func<Task<T>> loader, TimeSpan? ttl = null)
        {
            // Fast path: check cache without locking
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                return (T)entry.Value;
            }

            // Slow path: acquire per-key lock to prevent thundering herd
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_cache.TryGetValue(key, out entry) && !entry.IsExpired)
                {
                    return (T)entry.Value;
                }

                var value = await loader();
                var expiration = ttl ?? DefaultTtl;

                _cache[key] = new CacheEntry
                {
                    Value = value,
                    ExpiresAt = DateTime.UtcNow.Add(expiration)
                };

                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Invalidate(string key)
        {
            _cache.TryRemove(key, out _);
        }

        public void InvalidatePrefix(string prefix)
        {
            foreach (var key in _cache.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
