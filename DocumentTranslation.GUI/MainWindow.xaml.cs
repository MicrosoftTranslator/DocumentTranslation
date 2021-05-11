﻿using System;
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
        private ViewModel ViewModel;
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
        }

    }
}