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

        public static UISettings Read(string filename = null)
        {
            string appsettingsJson;
            try
            {
                if (string.IsNullOrEmpty(filename)) filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
                appsettingsJson = File.ReadAllText(filename);
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

        public static void Write(string filename, UISettings settings)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
            }
            File.WriteAllText(filename, JsonSerializer.Serialize(settings, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        }
    }

    public class UISettings
    {
        public string lastFromLanguage;
        public string lastToLanguage;
        public string lastFromLanguageDocuments;
        public List<string> lastToLanguagesDocuments;
        public string lastCategoryText;
        public string lastCategoryDocuments;
        public string lastDocumentsFolder;
        public Dictionary<string, PerLanguageData> PerLanguageFolders;
    }
    public class PerLanguageData
    {
        public string lastGlossariesFolder;
        public string lastGlossary;
        public string lastTargetFolder;
    }
}
