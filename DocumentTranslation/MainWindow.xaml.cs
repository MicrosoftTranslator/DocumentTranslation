using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DocumentTranslationService.Core;

namespace DocumentTranslation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DocumentTranslationService.Core.DocumentTranslationService documentTranslationService;
        public MainWindow()
        {
            InitializeComponent();
        }


        private void translateButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private static async Task Window_Loaded(object sender, RoutedEventArgs e)
        {
            DocTransAppSettings settings = await AppSettingsSetter.Read();
            DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
            documentTranslationService.OnLanguagesUpdate += DocumentTranslationService_OnLanguagesUpdate;
            Task task = documentTranslationService.InitializeAsync();
            TextTranslationService textTranslationService = new(documentTranslationService);
        }

        private static void DocumentTranslationService_OnLanguagesUpdate(object sender, EventArgs e)
        {
            var x = fromLanguageBox.Items; 

        }

        private void fromLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
