/*
 * Holds the configuration information for the document translation CLI
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentTranslation.GUI
{
    /// <summary>
    /// Manage the storage of the application settings
    /// </summary>
    public static class UISettingsSetter
    {
        const string AppName = "Document Translation";
        const string AppSettingsFileName = "uisettings.json";

        /// <summary>
        /// Create JSON string for app settings.
        /// </summary>
        /// <returns>JSON for appsettings</returns>
        public static string GetJson(UISettings settings)
        {
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true });
        }

        public static async Task<UISettings> Read(string filename = null)
        {
            string appsettingsJson;
            try
            {
                if (string.IsNullOrEmpty(filename)) filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
                appsettingsJson = await File.ReadAllTextAsync(filename);
            }

            catch (FileNotFoundException)
            {
                return new UISettings();
            }
            catch (DirectoryNotFoundException)
            {
                return new UISettings();
            }

            return JsonSerializer.Deserialize<UISettings>(appsettingsJson, new JsonSerializerOptions { IncludeFields = true });
        }

        public static async Task WriteAsync(string filename, UISettings settings)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
            }
            await File.WriteAllTextAsync(filename, GetJson(settings));
        }
    }

    public class UISettings
    {
        public string lastFromLanguage;
        public string lastToLanguage;
        public string lastFromLanguageDocuments;
        public string lastToLanguageDocuments;
        public string lastCategory;
        public string lastDocumentsFolder;
        public List<MyCategory> MyCategories;
        public List<PerLanguageData> PerLanguageFolders;
    }
    public class PerLanguageData
    {
        public string languageCode;
        public string lastGlossariesFolder;
        public string lastGlossary;
        public string lastTargetFolder;
    }


    public class MyCategory
    {
        /// <summary>
        /// Azure storage connection string, copied from the portal.
        /// </summary>
        public string CategoryID;
        public string MyCategoryName;
    }
}
