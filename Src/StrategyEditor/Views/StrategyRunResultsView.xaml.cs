using System.Windows.Controls;
using StrategyEditor.ViewModels;

namespace StrategyEditor.Views
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