using System.Windows;
using TraderTools.AutomatedTraderAI.ViewModels;

namespace TraderTools.AutomatedTraderAI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();
            SetWindowSize();
        }

        private void SetWindowSize()
        {
            Height = SystemParameters.PrimaryScreenHeight * 0.70;
            Width = Height * 1.5;
        }
    }
}