/*
 * Holds the configuration information for the document translation CLI
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DocumentTranslationService.Core
{
    /// <summary>
    /// Manage the storage of the application settings
    /// </summary>
    public static class AppSettingsSetter
    {
        public static event EventHandler SettingsReadComplete;
        const string AppName = "Document Translation";
        const string AppSettingsFileName = "appsettings.json";

        /// <summary>
        /// Create JSON string for app settings.
        /// </summary>
        /// <returns>JSON for appsettings</returns>
        public static string GetJson(DocTransAppSettings settings)
        {
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true });
        }

        /// <summary>
        /// Reads settings from file and from Azure KeyVault, if file settings indicate a key vault
        /// </summary>
        /// <param name="filename">File name to read settings from</param>
        /// <returns>Task</returns>
        /// <exception cref="KeyVaultAccessException" />
        public static DocTransAppSettings Read(string filename = null)
        {
            string appsettingsJson;
            try
            {
                if (string.IsNullOrEmpty(filename)) filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
                appsettingsJson = File.ReadAllText(filename);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    DocTransAppSettings settings = new();
                    settings.ConnectionStrings = new Connectionstrings();
                    settings.AzureRegion = "global";
                    return settings;
                }
                throw;
            }
            DocTransAppSettings result = JsonSerializer.Deserialize<DocTransAppSettings>(appsettingsJson, new JsonSerializerOptions { IncludeFields = true });
            if (!string.IsNullOrEmpty(result.AzureKeyVaultName))
            {
                Debug.WriteLine($"Authentication: Using Azure Key Vault {result.AzureKeyVaultName} to read credentials.");
            }
            else
            {
                Debug.WriteLine("Authentication: Using appsettings.json file to read credentials.");
                SettingsReadComplete?.Invoke(null, EventArgs.Empty);
            }
            if (result.AzureRegion is null) result.AzureRegion = "global";
            return result;
        }

        public static void Write(string filename, DocTransAppSettings settings)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
            }
            File.WriteAllText(filename, GetJson(settings));
            return;
        }


        /// <summary>
        /// Throws an exception to indicate the missing settings value;
        /// </summary>
        /// <param name="settings">The settings object to check on</param>
        /// <exception cref="ArgumentException"/>
        public static void CheckSettings(DocTransAppSettings settings, bool textOnly = false)
        {
            if (string.IsNullOrEmpty(settings.SubscriptionKey)) throw new ArgumentException("SubscriptionKey");
            if (string.IsNullOrEmpty(settings.AzureRegion)) throw new ArgumentException("AzureRegion");
            if (!textOnly)
            {
                if (string.IsNullOrEmpty(settings.ConnectionStrings.StorageConnectionString)) throw new ArgumentException("StorageConnectionString");
                if (string.IsNullOrEmpty(settings.AzureResourceName)) throw new ArgumentException("AzureResourceName");
            }
            return;
        }
    }
}
