using System;
using Sitecore.Configuration;

namespace Sitecore.React
{
	public static class Constants
	{
		public const string ExtensionsAssemblyFileName = "Sitecore.Pathfinder.Checker.dll";

		public const string SafeProjectUniqueIdRegex = @"[^a-zA-Z0-9_\.]";

		public static readonly char[] Comma =
		{
			','
		};
	}

    public struct SitecoreCustomCache
    {
        public static string CacheName = "Sitecore.React.CustomCache";

        public static bool Enabled => bool.Parse(Settings.GetSetting("Sitecore.React.CustomCache.Enabled", "true"));

        public static long CacheSize => StringUtil.ParseSizeString(Settings.GetSetting("Sitecore.React.CustomCache.MaxSize", "10MB"));

    }
}