using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using DocumentTranslationService.Core;

namespace CollectionView
{

}

namespace DocumentTranslation.GUI
{
    class ViewModel
    {
        internal class MyCategory
        {
            internal string Name;
            internal string ID;

            public MyCategory(string name, string iD)
            {
                Name = name;
                ID = iD;
            }
        }
        public ObservableCollection<Language> toLanguageList { get; private set; } = new();
        public ObservableCollection<Language> fromLanguageList { get; private set; } = new();
        public ObservableCollection<MyCategory> myCategoryList { get; private set; } = new();
        internal UISettings UISettings = new();
        public DocTransAppSettings settings { get; private set; } = new();
        public ObservableCollection<AzureRegion> azureRegions { get; private set; } = new();
        internal TextTranslationService textTranslationService;
        public Language FromLanguage { get; set; }
        public Language ToLanguage { get; set; }
        public List<string> FilesToTranslate { get => filesToTranslate; set => filesToTranslate = value; }
        public string TargetFolder { get; internal set; }
        public List<string> GlossariesToUse { get; internal set; } = new();

        private List<string> filesToTranslate = new();
        internal DocumentTranslationService.Core.DocumentTranslationService documentTranslationService;

        public ViewModel()
        {
        }

        public async Task Initialize()
        {
            settings = await AppSettingsSetter.Read();
            DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
            this.documentTranslationService = documentTranslationService;
            documentTranslationService.OnLanguagesUpdate += DocumentTranslationService_OnLanguagesUpdate;
            _ = documentTranslationService.GetLanguagesAsync();
            textTranslationService = new(documentTranslationService);
            UISettings = await UISettingsSetter.Read();
            if (UISettings.PerLanguageFolders is null) UISettings.PerLanguageFolders = new Dictionary<string, PerLanguageData>();
            if (UISettings.MyCategories is not null)
                foreach (var cat in UISettings.MyCategories.OrderBy((x) => x.MyCategoryName))
                    myCategoryList.Add(new MyCategory(cat.MyCategoryName, cat.CategoryID));
            _ = documentTranslationService.GetFormatsAsync();
            _ = documentTranslationService.GetGlossaryFormatsAsync();
        }

        public async Task SaveAsync()
        {
            List<Task> tasks = new();
            tasks.Add(UISettingsSetter.WriteAsync(null, UISettings));
            tasks.Add(AppSettingsSetter.WriteAsync(null, settings));
            await Task.WhenAll();
        }

        private void DocumentTranslationService_OnLanguagesUpdate(object sender, EventArgs e)
        {
            toLanguageList.Clear();
            fromLanguageList.Clear();
            fromLanguageList.Add(new Language("auto", "Auto-Detect"));
            var list = documentTranslationService.Languages.OrderBy((x) => x.Value.Name);
            foreach (var lang in list)
            {
                toLanguageList.Add(lang.Value);
                fromLanguageList.Add(lang.Value);
            }
        }

        internal async Task<string> TranslateTextAsync(string text, string fromLanguageCode, string toLanguageCode)
        {
            if (fromLanguageCode == "auto") fromLanguageCode = null;
            textTranslationService.AzureRegion = settings.AzureRegion;
            string result;
            try
            {
                result = await textTranslationService.TranslateStringAsync(text, fromLanguageCode, toLanguageCode);
                Debug.WriteLine($"Translate {text.Length} characters from {fromLanguageCode} to {toLanguageCode}");
            }
            catch (AccessViolationException ex)
            {
                result = ex.Message;
            }

            return result;
        }

        internal async Task<string> GetDocumentExtensionsFilter()
        {
            await documentTranslationService.GetFormatsAsync();
            StringBuilder filterBuilder = new();
            filterBuilder.Append("Document Translation|");
            foreach (var format in documentTranslationService.FileFormats.value)
            {
                foreach (var ext in format.fileExtensions)
                {
                    filterBuilder.Append("*" + ext + ";");
                }
            }
            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            filterBuilder.Append('|');

            foreach (var format in documentTranslationService.FileFormats.value)
            {
                filterBuilder.Append(format.format + "|");
                foreach (var ext in format.fileExtensions)
                {
                    filterBuilder.Append("*" + ext + ";");
                }
                filterBuilder.Remove(filterBuilder.Length - 1, 1);
                filterBuilder.Append('|');
            }
            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            return filterBuilder.ToString();
        }

        internal async Task<string> GetGlossaryExtensionsFilter()
        {
            await documentTranslationService.GetGlossaryFormatsAsync();
            StringBuilder filterBuilder = new();
            filterBuilder.Append("Glossaries|");
            foreach (var format in documentTranslationService.GlossaryFormats.value)
            {
                foreach (var ext in format.fileExtensions)
                {
                    filterBuilder.Append("*" + ext + ";");
                }
            }
            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            return filterBuilder.ToString();
        }

        public async Task GetAzureRegions()
        {
            AzureRegionsList azureRegionsList = new();
            List<AzureRegion> azureRegions = await azureRegionsList.GetAzureRegions();
            foreach (var region in azureRegions)
                this.azureRegions.Add(region);
        }
    }
}
