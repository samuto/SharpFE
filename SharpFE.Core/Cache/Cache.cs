﻿//-----------------------------------------------------------------------
// <copyright file="?.cs" company="Iain Sproat">
//     Copyright Iain Sproat, 2013.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace SharpFE.Cache
{
    /// <summary>
    /// Very simple cache implementation
    /// </summary>
    public class Cache<TKey, TValue>
    {
        private IDictionary<TKey, CachedValue<TValue>> internalStore = new Dictionary<TKey, CachedValue<TValue>>();
        
        public Cache()
        {
            // empty
        }
        
        public bool ContainsKey(TKey key)
        {
            return this.internalStore.ContainsKey(key);
        }
        
        public bool ContainsKey(TKey key, out TValue cachedValue)
        {
            return this.ContainsKey(key, false, 0, out cachedValue);
        }
        
        public bool ContainsKey(TKey key, int validHash, out TValue cachedValue)
        {
            return this.ContainsKey(key, true, validHash, out cachedValue);
        }
        
        private bool ContainsKey(TKey key, bool hashWasProvided, int validHash, out TValue cachedValue)
        {
            cachedValue = default(TValue);
            
            bool cachedResultExistsForThisKey = this.internalStore.ContainsKey(key);
            
            if (cachedResultExistsForThisKey)
            {
                CachedValue<TValue> cacheResult = this.internalStore[key];
                cachedValue = cacheResult.Value;
                if (hashWasProvided && cacheResult.IsValid(validHash))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public bool Save(TKey key, TValue value, int hashForValidityOfValue)
        {
            CachedValue<TValue> valueToCache = new CachedValue<TValue>(hashForValidityOfValue, value);
            if (this.ContainsKey(key))
            {
                // update cache with new value
                this.internalStore[key] = valueToCache;
            }
            else
            {
                // store new value in cache
                this.internalStore.Add(key, valueToCache);
            }
            
            return true;
        }
    }
}
