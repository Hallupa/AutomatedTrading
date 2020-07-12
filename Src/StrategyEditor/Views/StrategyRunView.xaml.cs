using System.Windows.Controls;
using StrategyEditor.ViewModels;

namespace StrategyEditor.Views
{
    /// <summary>
    /// Interaction logic for StrategyRunView.xaml
    /// </summary>
    public partial class StrategyRunView : Page
    {
        public StrategyRunView()
        {
            InitializeComponent();

            DataContext = new StrategyRunViewModel();
        }
    }
}