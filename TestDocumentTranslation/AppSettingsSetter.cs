/*
 * Holds the configuration information for the document translation CLI
 */

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TranslationService.CLI
{
    public static class AppSettingsSetter
    {
        /// <summary>
        /// Create JSON string for app settings.
        /// </summary>
        /// <returns>JSON for appsettings</returns>
        public static string GetJson(DocTransAppSettings settings)
        {
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true });
        }

        public static async Task<DocTransAppSettings> Read(string filename = "appsettings.json")
        {
            if (string.IsNullOrEmpty(filename)) filename = "appsettings.json";
            string appsettings;
            try
            {
                appsettings = await File.ReadAllTextAsync(filename);
            }
            catch (FileNotFoundException)
            {
                return new DocTransAppSettings();
            }
            return JsonSerializer.Deserialize<DocTransAppSettings>(appsettings, new JsonSerializerOptions { IncludeFields = true });
        }

        public static async Task Write(string filename, DocTransAppSettings settings)
        {
            if (string.IsNullOrEmpty(filename)) filename = "appsettings.json";
            await File.WriteAllTextAsync(filename, GetJson(settings));
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
