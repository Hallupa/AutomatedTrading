using System.Windows.Controls;
using AutomatedTrader.ViewModels;

namespace AutomatedTrader.Views
{
    /// <summary>
    /// Interaction logic for StrategyCustomRunView.xaml
    /// </summary>
    public partial class StrategyCustomRunView : Page
    {
        public StrategyCustomRunView()
        {
            InitializeComponent();

            DataContext = new StrategyCustomRunViewModel();
        }
    }
}