using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Abt.Controls.SciChart.Visuals;
using Hallupa.Library;
using log4net;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;

namespace AutomatedTrader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Info("Starting application");

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"AutomatedTrader");
            BrokersService.DataDirectory = path;

            DependencyContainer.AddAssembly(typeof(App).Assembly);
            DependencyContainer.AddAssembly(typeof(BrokersService).Assembly);
            DependencyContainer.AddAssembly(typeof(ChartingService).Assembly);
        }
    }
}
