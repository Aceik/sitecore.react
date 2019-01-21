namespace Sitecore.React.Configuration
{
    public class ReactSettings : IReactSettings
    {
        public bool UseDebugReactScript { get; set; }
        public string DynamicPlaceholderType { get; set; }
        public string BundleName { get; set; }
        public bool EnableClientside { get; set; }
        public bool EnableGroupedClientsideScripts { get; set; }
        public bool EnableDeferClientsideScripts { get; set; }
        public bool DisableClientSideWhenEditing { get; set; }
        public string BundleType { get; set; }
        public string ServerScript { get; set; }
        public string ClientScript { get; set; }
        public string Exception1RenderingId { get; set; }
        public string Exception1MinifiedInlineJs { get; set; }
        public bool Exception1Enabled { get; set; }
    }
}