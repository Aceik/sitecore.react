namespace Sitecore.React.Configuration
{
    public interface IReactSettings
    {
        bool UseDebugReactScript { get; set; }
        string DynamicPlaceholderType { get; set; }
        string BundleName { get; set; }
        bool EnableClientside { get; set; }
        bool EnableGroupedClientsideScripts { get; set; }
        bool EnableDeferClientsideScripts { get; set; }
        bool DisableClientSideWhenEditing { get; set; }

        bool Exception1Enabled { get; set; }
        string Exception1RenderingId { get; set; }
        string Exception1MinifiedInlineJs { get; set; }
        string BundleType { get; set; }
        string ServerScript { get; set; }
        string ClientScript { get; set; }

        bool LayoutServerSideOnly { get; set; }
        string LayoutName { get; set; }
    }
}
