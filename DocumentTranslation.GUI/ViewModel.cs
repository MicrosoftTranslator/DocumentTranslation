using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentTranslationService.Core;

namespace DocumentTranslation.GUI
{
    class ViewModel
    {
        internal ObservableCollection<string> toLanguageList = new();
        internal ObservableCollection<string> fromLanguageList = new();

        internal DocumentTranslationService.Core.DocumentTranslationService DocumentTranslationService;

        public ViewModel()
        {
        }

        public async Task Initialize()
        {
            DocTransAppSettings settings = await AppSettingsSetter.Read();
            DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
            DocumentTranslationService = documentTranslationService;
            documentTranslationService.OnLanguagesUpdate += DocumentTranslationService_OnLanguagesUpdate;
            Task task = documentTranslationService.InitializeAsync();
            TextTranslationService textTranslationService = new(documentTranslationService);
        }

        private void DocumentTranslationService_OnLanguagesUpdate(object sender, EventArgs e)
        {
            toLanguageList.Clear();
            fromLanguageList.Clear();
            foreach (var item in DocumentTranslationService.Languages)
                toLanguageList.Add(item.Value.Name);
            foreach (var item in DocumentTranslationService.Languages)
                fromLanguageList.Add(item.Value.Name);

        }
    }
}
