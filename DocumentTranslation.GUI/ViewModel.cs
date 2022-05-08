using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocumentTranslationService.Core;

namespace DocumentTranslation.GUI
{
    internal class ViewModel
    {
        public BindingList<Language> ToLanguageList { get; private set; } = new();
        public BindingList<Language> FromLanguageList { get; private set; } = new();
        internal static UISettings UISettings;
        public BindingList<Language> ToLanguageListForDocuments { get; private set; } = new();
        public BindingList<Language> FromLanguageListForDocuments { get; private set; } = new();

        public static DocTransAppSettings Settings
        {
            get => localSettings.UsingKeyVault ? keyVaultSettings : localSettings;
            set => localSettings = value;
        }
        internal static DocTransAppSettings localSettings;
        private static DocTransAppSettings keyVaultSettings;

        public Language FromLanguage { get; set; }
        public Language ToLanguage { get; init; }
        public BindingList<string> FilesToTranslate { get; private set; } = new();
        public string TargetFolder { get; set; }
        public string ErrorsText { get; set; }

        public BindingList<string> GlossariesToUse { get; set; }
        public event EventHandler OnLanguagesUpdate;
        public event EventHandler OnKeyVaultAuthenticationStart;
        public event EventHandler OnKeyVaultAuthenticationComplete;
        public event EventHandler<string> OnLanguagesFailed;

        internal DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new();
        public readonly Categories categories = new();

        public ViewModel()
        {
            localSettings = AppSettingsSetter.Read();
            UISettings = UISettingsSetter.Read();
            if (UISettings.PerLanguageFolders is null) UISettings.PerLanguageFolders = new Dictionary<string, PerLanguageData>();
            if (UISettings.lastToLanguagesDocuments is null) UISettings.lastToLanguagesDocuments = new List<string>();
        }

        /// <summary>
        /// Initializes the document translation service. Call once per instance.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="KeyVaultAccessException"/>
        public async Task InitializeAsync(bool IsTest=false)
        {
            documentTranslationService.OnLanguagesUpdate += DocumentTranslationService_OnLanguagesUpdate;
            documentTranslationService.ShowExperimental = localSettings.ShowExperimental;
            if (string.IsNullOrEmpty(documentTranslationService.TextTransUri)) documentTranslationService.TextTransUri = "https://api.cognitive.microsofttranslator.com/";
            try
            {
                _ = documentTranslationService.GetLanguagesAsync();   //this method can be called without credentials, and before the document translation service is initialized with credentials.
            }
            catch (Exception ex)
            {
                OnLanguagesFailed?.Invoke(this, ex.Message);
            }
            if (localSettings.UsingKeyVault)
            {
                Debug.WriteLine($"Start authenticating Key Vault {localSettings.AzureKeyVaultName}");
                if (!IsTest) OnKeyVaultAuthenticationStart?.Invoke(this, EventArgs.Empty);
                KeyVaultAccess kv = new(localSettings.AzureKeyVaultName);
                keyVaultSettings = await kv.GetKVCredentialsAsync();
                Debug.WriteLine($"Authentication Complete {localSettings.AzureKeyVaultName}");
                if (!IsTest) OnKeyVaultAuthenticationComplete?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                try
                {
                    AppSettingsSetter.CheckSettings(Settings);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentNullException(e.ParamName);
                }
            }

            documentTranslationService.SubscriptionKey = Settings.SubscriptionKey;
            documentTranslationService.AzureRegion= Settings.AzureRegion;
            documentTranslationService.AzureResourceName = Settings.AzureResourceName;
            documentTranslationService.StorageConnectionString = Settings.ConnectionStrings.StorageConnectionString;
            documentTranslationService.TextTransUri = Settings.TextTransEndpoint;
            documentTranslationService.FlightString = Settings.FlightString;
            try
            {
                _ = this.documentTranslationService.InitializeAsync();
            }
            catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException ex)
            {
                throw new DocumentTranslationService.Core.DocumentTranslationService.CredentialsException(ex.Message, ex);
            }
            return;
        }

        public static void SaveUISettings()
        {
            UISettingsSetter.Write(null, UISettings);
        }

        public static void SaveAppSettings()
        {
            AppSettingsSetter.Write(null, localSettings);
        }

        private void DocumentTranslationService_OnLanguagesUpdate(object sender, EventArgs e)
        {
            //Document translation does not support experimental languages. Maintain two separate language lists between document and text translation
            ToLanguageList.Clear();
            FromLanguageList.Clear();
            ToLanguageListForDocuments.Clear();
            FromLanguageListForDocuments.Clear();
            FromLanguageList.Add(new Language("auto", Properties.Resources.label_AutoDetect));
            FromLanguageListForDocuments.Add(new Language("auto", Properties.Resources.label_AutoDetect));
            var list = documentTranslationService.Languages.OrderBy((x) => x.Value.Name);
            foreach (var lang in list)
            {
                Language newLang = (Language)lang.Value.Clone();
                if (lang.Value.Experimental)
                    newLang.Name = lang.Value.Name + "  -" + Properties.Resources.label_Experimental + "-";
                ToLanguageList.Add(newLang);
                FromLanguageList.Add(newLang);
                if (!lang.Value.Experimental)
                {
                    ToLanguageListForDocuments.Add(lang.Value);
                    FromLanguageListForDocuments.Add(lang.Value);
                }
            }
            OnLanguagesUpdate?.Invoke(this, EventArgs.Empty);
        }

        internal async Task<string> TranslateTextAsync(string text, string fromLanguageCode, string toLanguageCode)
        {
            if (fromLanguageCode == "auto") fromLanguageCode = null;
            documentTranslationService.AzureRegion = Settings.AzureRegion;
            string result = await documentTranslationService.TranslateStringAsync(text, fromLanguageCode, toLanguageCode);
            Debug.WriteLine($"Translate {text.Length} characters from {fromLanguageCode} to {toLanguageCode}");
            return result;
        }

        #region Generate Filters
        internal async Task<string> GetDocumentExtensionsFilter()
        {
            StringBuilder filterBuilder = new();
            filterBuilder.Append(Properties.Resources.label_DocumentTranslation);
            await documentTranslationService.GetDocumentFormatsAsync();
            foreach (var format in documentTranslationService.FileFormats)
            {
                foreach (var ext in format.FileExtensions)
                {
                    filterBuilder.Append("*" + ext + ";");
                }
            }
            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            filterBuilder.Append('|');

            foreach (var format in documentTranslationService.FileFormats)
            {
                filterBuilder.Append(format.Format + "|");
                foreach (var ext in format.FileExtensions)
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
            StringBuilder filterBuilder = new();
            filterBuilder.Append(Properties.Resources.label_Glossaries);
            await documentTranslationService.GetGlossaryFormatsAsync();
            foreach (var format in documentTranslationService.GlossaryFormats)
            {
                foreach (var ext in format.FileExtensions)
                {
                    filterBuilder.Append("*" + ext + ";");
                }
            }
            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            return filterBuilder.ToString();
        }
        #endregion
        #region Settings.Categories

        internal void AddCategory(DataGridViewSelectedCellCollection selectedCells)
        {
            foreach (DataGridViewCell cell in selectedCells)
                categories.MyCategoryList.Insert(cell.RowIndex, new MyCategory(Properties.Resources.label_NewCategorySample, Properties.Resources.label_NewCategoryIDSample));
        }

        internal void DeleteCategory(DataGridViewSelectedCellCollection selectedCells)
        {
            foreach (DataGridViewCell cell in selectedCells)
                categories.MyCategoryList.RemoveAt(cell.RowIndex);
        }

        internal void SaveCategories()
        {
            categories.Write();
        }

        #endregion
    }
}
