using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace DocumentTranslationService.Core
{
    public class KeyvaultAccess
    {
        public string KeyVaultName { get; init; }

        public KeyvaultAccess(string keyVaultName)
        {
            KeyVaultName = keyVaultName;
        }

        /// <summary>
        /// Retrieve the Translator credentials from the key vault
        /// Caller should catch the Azure.RequestFailed exception.
        /// </summary>
        /// <returns></returns>
        public async Task<DocTransAppSettings> GetKVCredentialsAsync()
        {
            SecretClient client = new(new Uri("https://" + KeyVaultName + ".vault.azure.net/"), new DefaultAzureCredential());
            var regionTask = client.GetSecretAsync("AzureRegion");
            var resourceTask = client.GetSecretAsync("AzureResourceName");
            var storageTask = client.GetSecretAsync("StorageConnectionString");
            var keyTask = client.GetSecretAsync("SubscriptionKey");
            DocTransAppSettings settings = new();
            settings.AzureRegion = (await regionTask).Value.Value;
            settings.AzureResourceName = (await resourceTask).Value.Value;
            settings.ConnectionStrings.StorageConnectionString = (await storageTask).Value.Value;
            settings.SubscriptionKey = (await keyTask).Value.Value;
            return settings;
        }
    }
}
