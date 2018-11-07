using System;
using Sitecore;
using Sitecore.Caching;
using System.Web.Optimization;
using System.Collections.Generic;
using System.Linq;

namespace Sitecore.React.Cache
{
    public class PageBundle : ICacheable
    {
        public PageBundle()
        {
            Cacheable = true;
        }

        public string PageKey { get; set; }
        public bool Cacheable { get; set; }

        public bool Immutable => true;

        public Dictionary<string, string> ComponentBundle = new Dictionary<string, string>();

        public event DataLengthChangedDelegate DataLengthChanged
        {
            add { }
            remove { }
        }

        public long GetDataLength()
        {
            return ComponentBundle.Values.Sum(x => x.Length);
        }
    }
}