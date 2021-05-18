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
using System.Diagnostics;

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

        private async void DocumentBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            openFileDialog.Filter = await this.ViewModel.GetDocumentExtensionsFilter();
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.FilesToTranslate.Add(filename);
            FilesListBox.ItemsSource = ViewModel.FilesToTranslate;
            if ((ViewModel.FilesToTranslate.Count > 0) && (TargetListBox.Items.Count > 0)) translateDocumentsButton.IsEnabled = true;
            return;
        }

        private void ResetUI()
        {
            ProgressBar.Value = 0;
            StatusBarText1.Text = string.Empty;
            StatusBarText2.Text = string.Empty;
            CancelButton.IsEnabled = false;
            CancelButton.Background = Brushes.Gray;
            CancelButton.Visibility = Visibility.Visible;
            TargetOpenButton.Visibility = Visibility.Hidden;
        }

        private async void GlossariesBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            openFileDialog.Filter = await this.ViewModel.GetGlossaryExtensionsFilter();
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.GlossariesToUse.Add(filename);
            GlossariesListBox.ItemsSource = ViewModel.GlossariesToUse;
            return;
        }

        private void TargetOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(ViewModel.TargetFolder)) Process.Start("explorer.exe", ViewModel.TargetFolder);
        }

        private void TargetBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            List<string> items = new();
            FolderBrowserDialog folderBrowserDialog = new();
            folderBrowserDialog.ShowDialog();
            ViewModel.TargetFolder = folderBrowserDialog.SelectedPath;
            items.Add(ViewModel.TargetFolder);
            TargetListBox.ItemsSource = items;
            if ((ViewModel.FilesToTranslate.Count > 0) && (TargetListBox.Items.Count > 0)) translateDocumentsButton.IsEnabled = true;
        }

        private void DocumentsTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            CancelButton.IsEnabled = true;
            ProgressBar.IsIndeterminate = true;
            DocumentTranslationBusiness documentTranslationBusiness = new(ViewModel.documentTranslationService);
            documentTranslationBusiness.OnUploadComplete += DocumentTranslationBusiness_OnUploadComplete;
            documentTranslationBusiness.OnStatusUpdate += DocumentTranslationBusiness_OnStatusUpdate;
            documentTranslationBusiness.OnDownloadComplete += DocumentTranslationBusiness_OnDownloadComplete;
            _ = documentTranslationBusiness.RunAsync(ViewModel.FilesToTranslate, toLanguageBoxDocuments.SelectedValue as string, ViewModel.GlossariesToUse, ViewModel.TargetFolder);
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 1;
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBarText1.Text = "Canceling...";
            CancelButton.IsEnabled = false;
            CancelButton.Background = Brushes.Red;
            await ViewModel.documentTranslationService.CancelRunAsync();
            StatusBarText1.Text = "Canceled";
        }

        private void DocumentTranslationBusiness_OnUploadComplete(object sender, (int, long) e)
        {
            ProgressBar.Value = 10;
            StatusBarText1.Text = "Documents uploaded";
        }

        private void DocumentTranslationBusiness_OnStatusUpdate(object sender, StatusResponse e)
        {
            CancelButton.Background = Brushes.Gray;
            StatusBarText1.Text = e.status;
            StringBuilder statusText = new();
            if (e.summary.inProgress > 0) statusText.Append("In progress: " + e.summary.inProgress + '\t');
            if (e.summary.notYetStarted > 0) statusText.Append("Waiting: " + e.summary.notYetStarted + '\t');
            if (e.summary.success > 0) statusText.Append("Completed: " + e.summary.success + '\t');
            if (e.summary.failed > 0) statusText.Append("Failed: " + e.summary.failed);
            ProgressBar.Value = 10 +  ((e.summary.inProgress / ViewModel.FilesToTranslate.Count) * 0.2) + ((e.summary.success + e.summary.failed) / ViewModel.FilesToTranslate.Count * 0.85);
            StatusBarText2.Text = statusText.ToString();
        }


        private void DocumentTranslationBusiness_OnDownloadComplete(object sender, (int, long) e)
        {
            ProgressBar.Value = 100;
            StatusBarText1.Text = "Done";
            StatusBarText2.Text = $"{e.Item2} bytes in {e.Item1} documents translated";
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            TargetOpenButton.Visibility = Visibility.Visible;
        }
    }
}
