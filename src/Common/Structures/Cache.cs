/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Imlight.Common.Structures
{
    public class Cache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, CacheItem<TValue>> _cache = new Dictionary<TKey, CacheItem<TValue>>();

        public void Store(TKey key, TValue value, TimeSpan expiresAfter)
        {
            _cache[key] = new CacheItem<TValue>(value, expiresAfter);
        }

        public TValue Get(TKey key)
        {
            if (!_cache.ContainsKey(key)) return default(TValue);
            var cached = _cache[key];
            if (DateTimeOffset.Now - cached.Created >= cached.ExpiresAfter)
            {
                _cache.Remove(key);
                return default(TValue);
            }
            return cached.Value;
        }

        public TValue this[TKey key]
        {
            get => Get(key);
            set => Store(key, value, TimeSpan.MaxValue);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _cache.Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value)).GetEnumerator();
        }
        
        /// <summary>
        /// Remove a CacheItem from the cache.
        /// </summary>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            return _cache.Remove(key);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class CacheItem<T>
    {
        public CacheItem(T value, TimeSpan expiresAfter)
        {
            Value = value;
            ExpiresAfter = expiresAfter;
        }
        public T Value { get; }
        internal DateTimeOffset Created { get; } = DateTimeOffset.Now;
        internal TimeSpan ExpiresAfter { get; }
    }

}