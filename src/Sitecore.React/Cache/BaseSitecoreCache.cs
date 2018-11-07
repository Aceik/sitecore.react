using System;
using Sitecore.Caching;

namespace Sitecore.React.Cache
{
    public abstract class BaseSitecoreCache : CustomCache
    {
        private bool _cacheEnabled;
        protected BaseSitecoreCache(string name, long maxSize, bool cacheEnabled)
            : base(name, maxSize)
        {
            this._cacheEnabled = cacheEnabled;
        }
        public virtual bool CacheEnabled
        {
            get { return Context.PageMode.IsNormal && _cacheEnabled; }
            set {  _cacheEnabled = value; }
        }
        public void ClearCache(object sender, EventArgs args)
        {
            Clear();
        }
        public string Get(string cacheKey)
        {
            return !CacheEnabled ? string.Empty : GetString(cacheKey);
        }
        public T Get<T>(string cacheKey)
            where T : class
        {
            if (!CacheEnabled) return default(T);
            return (T)GetObject(cacheKey);
        }
        public string GetOrAddToCache(string cacheKey, Func<string> creator)
        {
            if (creator == null) return null;
            string returnValue;
            if (CacheEnabled)
            {
                returnValue = Get(cacheKey);
                if (!string.IsNullOrEmpty(returnValue)) return returnValue;
                returnValue = creator();
                Set(cacheKey, returnValue);
            }
            else
            {
                returnValue = creator();
            }
            return returnValue;
        }
        public T GetOrAddToCache<T>(string cacheKey, Func<T> creator)
            where T : class, ICacheable, new()
        {
            T returnValue;
            if (creator == null) return null;
            if (CacheEnabled)
            {
                returnValue = Get<T>(cacheKey);
                if (!string.IsNullOrEmpty(returnValue?.ToString())) return returnValue;
                returnValue = creator();
                SetObject(cacheKey, returnValue);
            }
            else
            {
                returnValue = creator();
            }
            return returnValue;
        }
        public void Set(string cacheKey, string value)
        {
            if (CacheEnabled && !string.IsNullOrEmpty(value)) SetString(cacheKey, value);
        }
        public void Set<T>(string cacheKey, T value)
            where T : ICacheable
        {
            if (!CacheEnabled) return;
            if (value != null && !string.IsNullOrEmpty(value.ToString())) SetObject(cacheKey, value);
        }
    }
}