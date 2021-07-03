using System.Windows.Controls;
using StrategyEditor.ViewModels;

namespace StrategyEditor.Views
{
    /// <summary>
    /// Interaction logic for StrategyViewAllTradesView.xaml
    /// </summary>
    public partial class StrategyViewAllTradesView : Page
    {
        public StrategyViewAllTradesView()
        {
            InitializeComponent();

            DataContext = new StrategyViewAllTradesViewModel();
        }
    }
}