/*
 * Holds the configuration information for the document translation settings
 */

namespace DocumentTranslationService.Core
{
    public class DocTransAppSettings
    {
        /// <summary>
        /// Name of the Azure Translator resource
        /// </summary>
        public string AzureResourceName { get; set; }
        /// <summary>
        /// Hold sthe connection strings.
        /// </summary>
        public Connectionstrings ConnectionStrings { get; set; }
        /// <summary>
        /// The subscription key to use.
        /// </summary>
        public string SubscriptionKey { get; set; }
        /// <summary>
        /// Whether to show experimental languages
        /// </summary>
        public bool ShowExperimental { get; set; }
        /// <summary>
        /// The Custom Translator category ID to use. 
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Hold the Azure region. Important only for text translation. This is the region ID, not the region friendly name.
        /// </summary>
        public string AzureRegion { get; set; }
        /// <summary>
        /// Holds the name of the Azure key vail to use instead of local settings.
        /// If not null or empty, other secrets and region will be ignored. 
        /// </summary>
        public string AzureKeyVaultName { get; set; }
        public bool UsingKeyVault
        {
            get
            {
                if (string.IsNullOrEmpty(AzureKeyVaultName)) return false;
                else return true;
            }
        }
    }

    public class Connectionstrings
    {
        /// <summary>
        /// Azure storage connection string, copied from the portal.
        /// </summary>
        public string StorageConnectionString { get; set; }
    }
}
