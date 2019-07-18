using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using AutomatedTrader.ViewModels;
using Hallupa.Library;
using log4net;
using TraderTools.Core.Services;

namespace AutomatedTrader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Import]
        private BrokersService _brokersService;

        public MainWindow()
        {
            InitializeComponent();

            DependencyContainer.ComposeParts(this);

            DataContext = new MainWindowsViewModel();
            Closing += OnClosing;
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            _brokersService.Dispose();
        }
    }
}