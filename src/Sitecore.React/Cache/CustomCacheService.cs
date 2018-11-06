using System;
using Sitecore;
using Sitecore.Caching;
namespace Sitecore.React.Cache
{
    public class CustomCacheService : BaseSitecoreCache
    {
        public CustomCacheService() : base(
                Sitecore.React.SitecoreCustomCache.CacheName,
                Sitecore.React.SitecoreCustomCache.CacheSize,
                Sitecore.React.SitecoreCustomCache.Enabled)
        {
        }

        public new void ClearCache(object sender, EventArgs args)
        {
            base.ClearCache(sender, args);
        }
    }
}