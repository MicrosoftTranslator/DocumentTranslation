using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DocumentTranslationService.Core;

namespace DocumentTranslation.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ViewModel ViewModel;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel viewModel = new();
            ViewModel = viewModel;
            toLanguageBox.ItemsSource = ViewModel.toLanguageList;
            fromLanguageBox.ItemsSource = ViewModel.fromLanguageList;
            toLanguageBoxDocuments.ItemsSource = ViewModel.toLanguageList;
            fromLanguageBoxDocuments.ItemsSource = ViewModel.fromLanguageList;

        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.Initialize();
            toLanguageBox.SelectedValue = ViewModel.UISettings.lastToLanguage;
            if (ViewModel.UISettings.lastFromLanguage is not null)
                fromLanguageBox.SelectedValue = ViewModel.UISettings.lastFromLanguage;
            else fromLanguageBox.SelectedIndex = 0;
            toLanguageBoxDocuments.SelectedValue = ViewModel.UISettings.lastToLanguageDocuments;
            if (ViewModel.UISettings.lastFromLanguageDocuments is not null)
                fromLanguageBoxDocuments.SelectedValue = ViewModel.UISettings.lastFromLanguageDocuments;
            else fromLanguageBoxDocuments.SelectedIndex = 0;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ViewModel.UISettings.lastToLanguage = toLanguageBox.SelectedValue as string;
            ViewModel.UISettings.lastFromLanguage = fromLanguageBox.SelectedValue as string;
            ViewModel.UISettings.lastToLanguageDocuments = toLanguageBoxDocuments.SelectedValue as string;
            ViewModel.UISettings.lastFromLanguageDocuments = fromLanguageBoxDocuments.SelectedValue as string;
            ViewModel.UISettings.lastCategory = CategoryBox.Text;
            await ViewModel.SaveAsync();
        }

        private async void TabItemAuthentication_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.GetAzureRegions();
            subscriptionKey.Password = ViewModel.settings.SubscriptionKey;
            resourceName.Text = ViewModel.settings.AzureResourceName;
            storageConnection.Text = ViewModel.settings.ConnectionStrings.StorageConnectionString;
            region.ItemsSource = ViewModel.azureRegions;
            region.SelectedValue = ViewModel.settings.AzureRegion;
        }

        private async void TabItemAuthentication_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.settings.SubscriptionKey = subscriptionKey.Password;
            ViewModel.settings.AzureResourceName = resourceName.Text;
            ViewModel.settings.ConnectionStrings.StorageConnectionString = storageConnection.Text;
            ViewModel.settings.AzureRegion = region.SelectedValue as string;
            await ViewModel.SaveAsync();
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            outputBox.Text = await ViewModel.TranslateTextAsync(inputBox.Text, fromLanguageBox.SelectedValue as string, toLanguageBox.SelectedValue as string);
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            openFileDialog.Filter = await this.ViewModel.GetDocumentExtensionsFilter();
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.FilesToTranslate.Add(filename);
            FilesListBox.ItemsSource = ViewModel.FilesToTranslate;
            return;
        }
        private async void GlossariesBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            openFileDialog.Filter = await this.ViewModel.GetGlossaryExtensionsFilter();
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.FilesToTranslate.Add(filename);
            GlossariesListBox.ItemsSource = ViewModel.FilesToTranslate;
            return;
        }

        private async void DocumentsTranslateButton_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
