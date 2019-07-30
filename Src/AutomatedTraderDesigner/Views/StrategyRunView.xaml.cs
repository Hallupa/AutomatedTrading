using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            ViewModel = new StrategyRunViewModel();

            DataContext = ViewModel;

            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyDownEvent, new KeyEventHandler(UIElement_OnPreviewKeyDown), true);
        }

        public StrategyRunViewModel ViewModel { get; }

        private void UIElement_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                if (ViewModel.RunStrategyEnabled)
                {
                    ViewModel.RunStrategyCommand.Execute(null);
                }
            }
        }
    }
}