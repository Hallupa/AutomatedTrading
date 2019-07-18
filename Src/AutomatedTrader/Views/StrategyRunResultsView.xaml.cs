using System.Windows.Controls;
using AutomatedTrader.ViewModels;

namespace AutomatedTrader.Views
{
    /// <summary>
    /// Interaction logic for StrategyRunResultsView.xaml
    /// </summary>
    public partial class StrategyRunResultsView : Page
    {
        public StrategyRunResultsView()
        {
            InitializeComponent();

            DataContext = new StrategyRunResultsViewModel();
        }
    }
}