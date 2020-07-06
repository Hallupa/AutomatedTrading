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

            DataContext = new StrategyRunViewModel();
        }
    }
}