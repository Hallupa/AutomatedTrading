using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using StrategyEditor.ViewModels;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;

namespace StrategyEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Import]
        private IBrokersService _brokersService;

        public MainWindow()
        {
            InitializeComponent();

            DependencyContainer.ComposeParts(this);

            DataContext = new MainWindowsViewModel();
            Closing += OnClosing;
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            ((IDisposable)_brokersService).Dispose();
        }
    }
}