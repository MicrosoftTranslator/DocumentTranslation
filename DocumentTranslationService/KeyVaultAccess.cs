using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

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
            string VaultUri;
            if (KeyVaultName.Contains('.')) VaultUri = KeyVaultName;
            else VaultUri = "https://" + KeyVaultName + ".vault.azure.net/";
            SecretClient client = new(new Uri(VaultUri), new InteractiveBrowserCredential());
            List<string> secretNames = new() { "AzureRegion", "DocTransEndpoint", "StorageConnectionString", "ResourceKey", "TextTransEndpoint" };
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Azure Key Vault: {ex.Message}");
                throw new KeyVaultAccessException("msg_KeyVaultRequestFailed", ex);
            }
            DocTransAppSettings settings = new();
            foreach (var kvSecret in kvSecrets)
            {
                switch (kvSecret.Value.Name)
                {
                    case "AzureRegion":
                        settings.AzureRegion = kvSecret.Value.Value;
                        break;
                    case "DocTransEndpoint":
                        settings.AzureResourceName = kvSecret.Value.Value;
                        break;
                    case "StorageConnectionString":
                        settings.ConnectionStrings ??= new();
                        settings.ConnectionStrings.StorageConnectionString = kvSecret.Value.Value;
                        break;
                    case "ResourceKey":
                        settings.SubscriptionKey = kvSecret.Value.Value;
                        break;
                    case "TextTransEndpoint":
                        settings.TextTransEndpoint = kvSecret.Value.Value;
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
