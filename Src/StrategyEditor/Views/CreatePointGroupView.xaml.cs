using System.Windows;

namespace StrategyEditor.Views
{
    /// <summary>
    /// Interaction logic for CreatePointGroupView.xaml
    /// </summary>
    public partial class CreatePointGroupView : Window
    {
        public CreatePointGroupView()
        {
            InitializeComponent();
        }

        public bool OKClicked { get; private set; }

        private void OkClicked(object sender, RoutedEventArgs e)
        {
            OKClicked = true;
            Close();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}