using System.Windows.Controls;
using AutomatedTraderDesigner.ViewModels;

namespace AutomatedTraderDesigner.Views
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