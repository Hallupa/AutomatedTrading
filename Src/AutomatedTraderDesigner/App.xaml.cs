using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using Abt.Controls.SciChart.Visuals;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;
using TraderTools.Strategy;

namespace AutomatedTraderDesigner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Import] private IDataDirectoryService _dataDirectoryService;

        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Info("Starting application");

            DependencyContainer.AddAssembly(typeof(App).Assembly);
            DependencyContainer.AddAssembly(typeof(BrokersService).Assembly);
            DependencyContainer.AddAssembly(typeof(ChartingService).Assembly);
            DependencyContainer.AddAssembly(typeof(StrategyService).Assembly);

            DependencyContainer.ComposeParts(this);

            _dataDirectoryService.SetApplicationName("AutomatedTrader");
        }
    }
}