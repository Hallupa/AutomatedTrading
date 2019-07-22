using System.Windows.Controls;
using AutomatedTraderDesigner.ViewModels;

namespace AutomatedTraderDesigner.Views
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