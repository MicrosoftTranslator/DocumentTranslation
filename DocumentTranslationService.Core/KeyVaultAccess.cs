using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace DocumentTranslationService.Core
{
    public class KeyVaultAccess
    {
        public string KeyVaultName { get; init; }

        public KeyVaultAccess(string keyVaultName)
        {
            KeyVaultName = keyVaultName;
        }

        /// <summary>
        /// Retrieve the Translator credentials from the key vault
        /// Caller should catch the Azure.RequestFailed exception.
        /// </summary>
        /// <returns cref="DocTransAppSettings">DocTransAppSettings class</returns>
        /// <exception cref="KeyVaultAccessException"/>
        public async Task<DocTransAppSettings> GetKVCredentialsAsync()
        {
            SecretClient client = new(new Uri("https://" + KeyVaultName + ".vault.azure.net/"), new DefaultAzureCredential());
            List<string> secretNames = new() { "AzureRegion", "AzureResourceName", "StorageConnectionString", "SubscriptionKey" };
            List<Task<Azure.Response<KeyVaultSecret>>> tasks = new();
            Azure.Response<KeyVaultSecret>[] kvSecrets;
            foreach (string secret in secretNames) tasks.Add(client.GetSecretAsync(secret));
            try
            {
                kvSecrets = await Task.WhenAll(tasks);
            }
            catch (CredentialUnavailableException ex)
            {
                Debug.WriteLine($"Azure Key Vault: {ex.Message}\nPlease log in to your work or school account.");
                throw new KeyVaultAccessException("msg_NotLoggedIn", ex);
            }
            catch (Azure.RequestFailedException ex)
            {
                Debug.WriteLine($"Azure Key Vault: {ex.Message}");
                throw new KeyVaultAccessException("msg_KeyVaultRequestFailed", ex);
            }
            // catch more different exceptions here
            DocTransAppSettings settings = new();
            foreach (var kvSecret in kvSecrets)
            {
                switch (kvSecret.Value.Name)
                {
                    case "AzureRegion":
                        settings.AzureRegion = kvSecret.Value.Value;
                        break;
                    case "AzureResourceName":
                        settings.AzureResourceName = kvSecret.Value.Value;
                        break;
                    case "StorageConnectionString":
                        if (settings.ConnectionStrings is null) settings.ConnectionStrings = new();
                        settings.ConnectionStrings.StorageConnectionString = kvSecret.Value.Value;
                        break;
                    case "SubscriptionKey":
                        settings.SubscriptionKey = kvSecret.Value.Value;
                        break;
                    default:
                        break;
                }
            }
            settings.AzureKeyVaultName = KeyVaultName;
            return settings;
        }
    }
}
