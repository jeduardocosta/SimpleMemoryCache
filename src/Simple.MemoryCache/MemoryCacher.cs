﻿using System;
using System.Web;
using System.Web.Caching;
using Simple.MemoryCache.Converters;
using Simple.MemoryCache.DI;
using Simple.MemoryCache.Entities;

namespace Simple.MemoryCache
{
    public interface IMemoryCacher
    {
        void Add<T>(string key, T value, TimeSpan expiration);
        void Add<T>(string key, T value, CacheItemPolicy policy);

        T Retrieve<T>(string key);
        T RetrieveOrElse<T>(string key, TimeSpan expiration, Func<T> retrievalDelegate);
        T RetrieveOrElse<T>(string key, CacheItemPolicy policy, Func<T> retrievalDelegate);

        void Set<T>(string key, T value, TimeSpan expiration);
        void Set<T>(string key, T value, CacheItemPolicy policy);
    }

    public class MemoryCacher : IMemoryCacher
    {
        private readonly ICacheItemPolicyConverter _cacheItemPolicyConverter;
        private readonly ICacheItemPriorityConverter _cacheItemPriorityConverter;

        public MemoryCacher() : this(
                DIContainer.Resolve<ICacheItemPolicyConverter>(),
                DIContainer.Resolve<ICacheItemPriorityConverter>()) { }

        public MemoryCacher(ICacheItemPolicyConverter cacheItemPolicyConverter,
            ICacheItemPriorityConverter cacheItemPriorityConverter)
        {
            _cacheItemPolicyConverter = cacheItemPolicyConverter;
            _cacheItemPriorityConverter = cacheItemPriorityConverter;
        }

        private static Cache Cache
        {
            get { return HttpRuntime.Cache; }
        }

        public void Add<T>(string key, T value, TimeSpan expiration)
        {
            Cache.Add(key, value, default(CacheDependency), DateTime.MaxValue, expiration, System.Web.Caching.CacheItemPriority.Default, default(CacheItemRemovedCallback));
        }

        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            Cache.Remove(key);
            Cache.Add(key, value, default(CacheDependency), DateTime.MaxValue, expiration, System.Web.Caching.CacheItemPriority.Default, default(CacheItemRemovedCallback));
        }

        public void Set<T>(string key, T value, CacheItemPolicy policy)
        {
            var slidingExpiration = policy.SlidingExpiration.HasValue ? policy.SlidingExpiration.Value : default(TimeSpan);
            var absoluteExpiration = policy.AbsoluteExpiration.HasValue ? policy.AbsoluteExpiration.Value.DateTime : default(DateTime);
            var cacheItemPriority = _cacheItemPriorityConverter.Convert(policy.CacheItemPriority);
            
            Cache.Remove(key);
            Cache.Add(key, value, default(CacheDependency), absoluteExpiration, slidingExpiration, cacheItemPriority, default(CacheItemRemovedCallback));
        }

        public void Add<T>(string key, T value, CacheItemPolicy policy)
        {
            var slidingExpiration = TimeSpan.Zero;
            var absoluteExpiration = DateTime.MaxValue;
            var cacheItemPriority = _cacheItemPriorityConverter.Convert(policy.CacheItemPriority);

            if (policy.SlidingExpiration.HasValue)
                slidingExpiration = policy.SlidingExpiration.Value;
            if (policy.AbsoluteExpiration.HasValue)
                absoluteExpiration = policy.AbsoluteExpiration.Value.DateTime;

            Cache.Add(key, value, default(CacheDependency), absoluteExpiration, slidingExpiration, cacheItemPriority, default(CacheItemRemovedCallback));
        }

        public T RetrieveOrElse<T>(string key, CacheItemPolicy policy, Func<T> retrievalDelegate)
        {
            var cachedObject = Cache[key];

            if (cachedObject == null)
            {
                var retrievedObject = retrievalDelegate();
                Add(key, retrievedObject, policy);
                cachedObject = retrievedObject;
            }

            return (T)cachedObject;
        }

        public T RetrieveOrElse<T>(string key, TimeSpan expiration, Func<T> retrievalDelegate)
        {
            var policy = new CacheItemPolicy
                .CacheItemPolicyBuilder()
                .WithSlidingExpiration(expiration)
                .Build();

            return RetrieveOrElse(key, policy, retrievalDelegate);
        }

        public T Retrieve<T>(string key)
        {
            return (T)Cache[key];
        }
    }
}