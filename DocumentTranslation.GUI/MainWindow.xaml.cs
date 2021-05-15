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
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.Initialize();
            toLanguageBox.SelectedValue = ViewModel.UISettings.lastToLanguage;
            if (ViewModel.UISettings.lastFromLanguage is not null)
                fromLanguageBox.SelectedValue = ViewModel.UISettings.lastFromLanguage;
            else fromLanguageBox.SelectedIndex = 0;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ViewModel.UISettings.lastToLanguage = toLanguageBox.SelectedValue as string;
            ViewModel.UISettings.lastFromLanguage = fromLanguageBox.SelectedValue as string;
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

        private async void translateButton_Click(object sender, RoutedEventArgs e)
        {
            outputBox.Text = await ViewModel.textTranslationService.TranslateStringAsync(inputBox.Text, fromLanguageBox.SelectedValue as string, toLanguageBox.SelectedValue as string);
        }
    }
}
