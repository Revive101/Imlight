/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
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
