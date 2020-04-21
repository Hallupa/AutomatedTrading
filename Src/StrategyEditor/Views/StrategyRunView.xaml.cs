using System.Windows.Controls;
using AutomatedTraderDesigner.ViewModels;

namespace AutomatedTraderDesigner.Views
{
    /// <summary>
    /// Interaction logic for StrategyRunView.xaml
    /// </summary>
    public partial class StrategyRunView : Page
    {
        public StrategyRunView()
        {
            InitializeComponent();

            ViewModel = new StrategyRunViewModel();

            DataContext = ViewModel;

        }

        public StrategyRunViewModel ViewModel { get; }
    }
}