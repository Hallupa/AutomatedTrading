using System.Windows;
using AutomatedTrader.ViewModels;

namespace AutomatedTrader.Views
{
    /// <summary>
    /// Interaction logic for TradesView.xaml
    /// </summary>
    public partial class TradesView : Window
    {
        public TradesView()
        {
            InitializeComponent();

            DataContext = new TradesViewModel();
        }
    }
}