using System.Windows.Controls;
using StrategyEditor.ViewModels;

namespace StrategyEditor.Views
{
    /// <summary>
    /// Interaction logic for StrategyRunResultsChartView.xaml
    /// </summary>
    public partial class StrategyRunResultsChartView : Page
    {
        public StrategyRunResultsChartView()
        {
            InitializeComponent();

            DataContext = new StrategyRunResultsChartViewModel();
        }
    }
}