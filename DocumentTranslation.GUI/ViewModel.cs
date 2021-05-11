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
        internal ObservableCollection<string> myCategoryList = new();
        internal UISettings UISettings = new();

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
            Task task = documentTranslationService.GetLanguagesAsync();
            TextTranslationService textTranslationService = new(documentTranslationService);
            UISettings = await UISettingsSetter.Read();
            if (UISettings.MyCategories is not null)
                foreach (var item in UISettings.MyCategories.OrderBy((x) => x.MyCategoryName))
                    myCategoryList.Add(item.MyCategoryName);
        }

        public async Task Close()
        {
            await UISettingsSetter.Write(null, UISettings);
        }

        private void DocumentTranslationService_OnLanguagesUpdate(object sender, EventArgs e)
        {
            toLanguageList.Clear();
            var list = DocumentTranslationService.Languages.OrderBy((x) => x.Value.Name);
            foreach (var item in list)
                toLanguageList.Add(item.Value.Name);
            fromLanguageList.Clear();
            fromLanguageList.Add("Auto-Detect");
            foreach (var item in list)
                fromLanguageList.Add(item.Value.Name);
        }
    }
}
