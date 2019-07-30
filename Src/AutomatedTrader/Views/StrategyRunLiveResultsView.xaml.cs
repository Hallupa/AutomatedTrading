using System.Windows.Controls;
using AutomatedTrader.ViewModels;
using AutomatedTraderDesigner.ViewModels;

namespace AutomatedTrader.Views
{
    /// <summary>
    /// Interaction logic for StrategyRunLiveResultsView.xaml
    /// </summary>
    public partial class StrategyRunLiveResultsView : Page
    {
        public StrategyRunLiveResultsView()
        {
            InitializeComponent();

            DataContext = new StrategyRunLiveResultsViewModel();
        }
    }
}