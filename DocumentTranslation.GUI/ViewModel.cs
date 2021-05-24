using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocumentTranslationService.Core;

namespace CollectionView
{

}

namespace DocumentTranslation.GUI
{
    internal class ViewModel
    {
        public ObservableCollection<Language> ToLanguageList { get; private set; } = new();
        public ObservableCollection<Language> FromLanguageList { get; private set; } = new();
        internal UISettings UISettings = new();
        public DocTransAppSettings Settings { get; set; } = new();
        public ObservableCollection<AzureRegion> AzureRegions { get; private set; } = new();
        internal TextTranslationService textTranslationService;
        public Language FromLanguage { get; set; }
        public Language ToLanguage { get; set; }
        public List<string> FilesToTranslate { get => filesToTranslate; set => filesToTranslate = value; }
        public string TargetFolder { get; set; }
        public List<string> GlossariesToUse { get; private set; } = new();

        private List<string> filesToTranslate = new();
        internal DocumentTranslationService.Core.DocumentTranslationService documentTranslationService;
        public readonly Categories categories = new();

        public ViewModel()
        {
        }

        public async Task Initialize()
        {
            Settings = await AppSettingsSetter.Read();
            try
            {
                AppSettingsSetter.CheckSettings(Settings);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentNullException(e.ParamName);
            }
            DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(Settings.SubscriptionKey, Settings.AzureResourceName, Settings.ConnectionStrings.StorageConnectionString);
            this.documentTranslationService = documentTranslationService;
            documentTranslationService.OnLanguagesUpdate += DocumentTranslationService_OnLanguagesUpdate;
            _ = documentTranslationService.GetLanguagesAsync();
            textTranslationService = new(documentTranslationService);
            UISettings = await UISettingsSetter.Read();
            if (UISettings.PerLanguageFolders is null) UISettings.PerLanguageFolders = new Dictionary<string, PerLanguageData>();
            _ = documentTranslationService.GetDocumentFormatsAsync();
            _ = documentTranslationService.GetGlossaryFormatsAsync();

            return;
        }

        public async Task SaveAsync()
        {
            List<Task> tasks = new();
            tasks.Add(UISettingsSetter.WriteAsync(null, UISettings));
            tasks.Add(AppSettingsSetter.WriteAsync(null, Settings));
            await Task.WhenAll();
        }

        private void DocumentTranslationService_OnLanguagesUpdate(object sender, EventArgs e)
        {
            ToLanguageList.Clear();
            FromLanguageList.Clear();
            FromLanguageList.Add(new Language("auto", "Auto-Detect"));
            var list = documentTranslationService.Languages.OrderBy((x) => x.Value.Name);
            foreach (var lang in list)
            {
                ToLanguageList.Add(lang.Value);
                FromLanguageList.Add(lang.Value);
            }
        }

        internal async Task<string> TranslateTextAsync(string text, string fromLanguageCode, string toLanguageCode)
        {
            if (fromLanguageCode == "auto") fromLanguageCode = null;
            textTranslationService.AzureRegion = Settings.AzureRegion;
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

        #region Generate Filters
        internal async Task<string> GetDocumentExtensionsFilter()
        {
            await documentTranslationService.GetDocumentFormatsAsync();
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
        #endregion
        #region Credentials
        public async Task GetAzureRegions()
        {
            AzureRegionsList azureRegionsList = new();
            List<AzureRegion> azureRegions = await azureRegionsList.GetAzureRegions();
            foreach (var region in azureRegions)
                this.AzureRegions.Add(region);
        }
        #endregion
        #region Settings.Categories

        internal void AddCategory(DataGridViewSelectedCellCollection selectedCells)
        {
            foreach (DataGridViewCell cell in selectedCells)
                categories.MyCategoryList.Insert(cell.RowIndex, new MyCategory("New category name", "New category ID"));
        }

        internal void DeleteCategory(DataGridViewSelectedCellCollection selectedCells)
        {
            foreach (DataGridViewCell cell in selectedCells)
                categories.MyCategoryList.RemoveAt(cell.RowIndex);
        }

        internal async void SaveCategories()
        {
            await categories.WriteAsync();
        }
        #endregion
    }
}
