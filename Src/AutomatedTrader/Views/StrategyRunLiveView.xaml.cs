using System.Windows.Controls;
using AutomatedTrader.ViewModels;

namespace AutomatedTrader.Views
{
    /// <summary>
    /// Interaction logic for StrategyRunLiveView.xaml
    /// </summary>
    public partial class StrategyRunLiveView : Page
    {
        public StrategyRunLiveView()
        {
            InitializeComponent();

            DataContext = new StrategyRunLiveViewModel();
        }
    }
}