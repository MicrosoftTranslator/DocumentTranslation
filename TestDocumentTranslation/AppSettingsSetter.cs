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
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { IncludeFields = true });
        }

        public static async Task<DocTransAppSettings> Read(string filename)
        {
            if (string.IsNullOrEmpty(filename)) filename = "appsettings.json";
            string appsettings;
            try
            {
                appsettings = await File.ReadAllTextAsync(filename);
            }
            catch (FileNotFoundException)
            {
                return null;
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
        public string AzureResourceName { get; set; }
        public Connectionstrings ConnectionStrings { get; set; }
        public string SubscriptionKey { get; set; }
        public bool ShowExperimental { get; set; }
    }

    public class Connectionstrings
    {
        public string StorageConnectionString { get; set; }
    }
}
