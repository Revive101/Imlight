/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Shared.Structures;

public class Cache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> {

    private readonly Dictionary<TKey, CacheItem<TValue>> _cache = [];

    /// <summary>
    /// Store a CacheItem in the cache.
    /// </summary>
    /// <param name="key">The key to store the CacheItem under.</param>
    /// <param name="value">The value to store in the CacheItem.</param>
    /// <param name="expiresAfter">The time after which the CacheItem will expire.</param>
    public void Store(TKey key, TValue value, TimeSpan expiresAfter)
        => _cache[key] = new CacheItem<TValue>(value, expiresAfter);

    /// <summary>
    /// Get a CacheItem from the cache.
    /// </summary>
    /// <param name="key">The key to get the CacheItem from.</param>
    /// <returns>The value of the CacheItem, or default if it does not exist.</returns>
    public TValue Get(TKey key) {
        if (!_cache.TryGetValue(key, out var cached)) {
            return default;
        }

        if (DateTimeOffset.Now - cached.Created >= cached.ExpiresAfter) {
            _ = _cache.Remove(key);
            return default;
        }

        return cached.Value;
    }

    /// <summary>
    /// Get or set a CacheItem in the cache.
    /// </summary>
    /// <param name="key">The key to get or set the CacheItem under.</param>
    /// <returns>The value of the CacheItem, or default if it does not exist.</returns>
    public TValue this[TKey key] {
        get => Get(key);
        set => Store(key, value, TimeSpan.MaxValue);
    }

    /// <summary>
    /// Gets the enumerator for the cache.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() 
        => _cache.Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value)).GetEnumerator();

    /// <summary>
    /// Remove a CacheItem from the cache.
    /// </summary>
    /// <returns></returns>
    public bool Remove(TKey key) 
        => _cache.Remove(key);

    IEnumerator IEnumerable.GetEnumerator() 
        => GetEnumerator();

}

public class CacheItem<T>(T value, TimeSpan expiresAfter) {

    public T Value { get; } = value;
    internal DateTimeOffset Created { get; } = DateTimeOffset.Now;
    internal TimeSpan ExpiresAfter { get; } = expiresAfter;

}
