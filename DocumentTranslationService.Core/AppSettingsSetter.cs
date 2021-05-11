/*
 * Holds the configuration information for the document translation CLI
 */

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    /// <summary>
    /// Manage the storage of the application settings
    /// </summary>
    public static class AppSettingsSetter
    {
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

        public static async Task<DocTransAppSettings> Read(string filename = null)
        {
            string appsettingsJson;
            try
            {
                if (string.IsNullOrEmpty(filename)) filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
                appsettingsJson = await File.ReadAllTextAsync(filename);
            }

            catch (FileNotFoundException)
            {
                return new DocTransAppSettings();
            }
            catch (DirectoryNotFoundException)
            {
                return new DocTransAppSettings();
            }

            return JsonSerializer.Deserialize<DocTransAppSettings>(appsettingsJson, new JsonSerializerOptions { IncludeFields = true });
        }

        public static async Task Write(string filename, DocTransAppSettings settings)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
            }
            await File.WriteAllTextAsync(filename, GetJson(settings));
        }

        /// <summary>
        /// Throws an exception to indicate the missing settings value;
        /// </summary>
        /// <param name="settings"></param>
        public static void CheckSettings(DocTransAppSettings settings)
        {
            if (string.IsNullOrEmpty(settings.SubscriptionKey)) throw new ArgumentException("SubscriptionKey");
            if (string.IsNullOrEmpty(settings.ConnectionStrings.StorageConnectionString)) throw new ArgumentException("StorageConnectionString");
            if (string.IsNullOrEmpty(settings.AzureResourceName)) throw new ArgumentException("AzureResourceName");
        }
    }

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
    }

    public class Connectionstrings
    {
        /// <summary>
        /// Azure storage connection string, copied from the portal.
        /// </summary>
        public string StorageConnectionString { get; set; }
    }
}
