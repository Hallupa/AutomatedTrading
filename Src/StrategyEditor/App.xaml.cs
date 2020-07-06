using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Windows;
using Hallupa.Library;
using log4net;
using log4net.Config;
using TraderTools.Basics;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;
using TraderTools.Simulation;

namespace StrategyEditor
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
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            Log.Info("Starting application");

            DependencyContainer.AddAssembly(typeof(App).Assembly);
            DependencyContainer.AddAssembly(typeof(BrokersService).Assembly);
            DependencyContainer.AddAssembly(typeof(ChartingService).Assembly);
            DependencyContainer.AddAssembly(typeof(ModelPredictorService).Assembly);

            DependencyContainer.ComposeParts(this);

            _dataDirectoryService.SetApplicationName("AutomatedTrader");
        }
    }
}