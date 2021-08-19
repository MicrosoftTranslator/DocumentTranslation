using System.Windows;

namespace DocumentTranslation.GUI
{
    /// <summary>
    /// Interaction logic for ShowErrors.xaml
    /// </summary>
    public partial class ShowErrors : Window
    {
        public ShowErrors()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
