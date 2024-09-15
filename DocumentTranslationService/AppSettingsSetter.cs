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
                if (string.IsNullOrEmpty(filename)) filename = GetSettingsFilename();
                appsettingsJson = File.ReadAllText(filename);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    DocTransAppSettings settings = new()
                    {
                        ConnectionStrings = new Connectionstrings(),
                        AzureRegion = "global",
                        TextTransEndpoint = "https://api.cognitive.microsofttranslator.com/",
                        AzureResourceName = "https://*.cognitiveservices.azure.com/"
                    };
                    settings.ConnectionStrings.StorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=*";
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
            if (string.IsNullOrEmpty(result.AzureRegion)) result.AzureRegion = "global";
            return result;
        }

        public static void Write(string filename, DocTransAppSettings settings)
        {
            if (string.IsNullOrEmpty(filename))
            {
                filename = GetSettingsFilename();
            }
            try
            {
                File.WriteAllText(filename, GetJson(settings));
            }
            catch (IOException)
            {
                Console.WriteLine("ERROR: Failed writing settings to " + filename);
            }
            return;
        }

        private static string GetSettingsFilename()
        {
            string filename;
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
            }
            else
            {
                filename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + AppName + "_" + AppSettingsFileName;
            }

            return filename;
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
