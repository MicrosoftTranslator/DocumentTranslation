namespace DocumentTranslation.Web.Services
{
    public class DocumentTranslationSettings
    {
        public string AzureResourceName { get; set; } = string.Empty;
        public string SubscriptionKey { get; set; } = string.Empty;
        public string AzureRegion { get; set; } = string.Empty;
        public string StorageConnectionString { get; set; } = string.Empty;
        public string AzureKeyVaultName { get; set; } = string.Empty;
        public bool ShowExperimental { get; set; } = false;
        public string Category { get; set; } = string.Empty;
        public string TextTransEndpoint { get; set; } = string.Empty;
        public string FlightString { get; set; } = string.Empty;
    }
}
