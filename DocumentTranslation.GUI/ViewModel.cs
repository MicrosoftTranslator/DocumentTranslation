using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        internal ObservableCollection<Language> toLanguageList = new();
        internal ObservableCollection<Language> fromLanguageList = new();
        internal ObservableCollection<MyCategory> myCategoryList = new();
        internal UISettings UISettings = new();
        internal DocTransAppSettings settings = new();
        public ObservableCollection<AzureRegion> azureRegions = new();
        internal TextTranslationService textTranslationService;
        internal Language fromLanguage { get; set; }
        internal Language toLanguage { get; set; }

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
            Task task = documentTranslationService.GetLanguagesAsync();
            textTranslationService = new(documentTranslationService);
            UISettings = await UISettingsSetter.Read();
            if (UISettings.MyCategories is not null)
                foreach (var cat in UISettings.MyCategories.OrderBy((x) => x.MyCategoryName))
                    myCategoryList.Add(new MyCategory(cat.MyCategoryName, cat.CategoryID));
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

        public async Task GetAzureRegions()
        {
            AzureRegionsList azureRegionsList = new();
            List<AzureRegion> azureRegions = await azureRegionsList.GetAzureRegions();
            foreach (var region in azureRegions)
                this.azureRegions.Add(region);
        }
    }
}
