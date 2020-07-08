using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;
using TraderTools.Simulation;

namespace StrategyRunnerLive.ViewModels
{
    public class RunStrategyLiveViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private FxcmBroker _fxcm;
        private bool _runningLive = false;
        private string _strategiesDirectory;

        [Import] private IBrokersService _brokersService;
        [Import] private IDataDirectoryService _dataDirectoryService;

        public RunStrategyLiveViewModel()
        {
            DependencyContainer.ComposeParts(this);

            RunLiveCommand = new DelegateCommand(o => RunLive());
            _fxcm = (FxcmBroker) _brokersService.Brokers.First(x => x.Name == "FXCM");
            _strategiesDirectory = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName);
        }

        public DelegateCommand RunLiveCommand { get; }

        public string SelectedStrategyFilename { get; set; }

        private void RunLive()
        {
            if (_fxcm.Status != ConnectStatus.Connected)
            {
                MessageBox.Show(Application.Current.MainWindow, "FXCM not connected", "Cannot run live");
                return;
            }

            if (string.IsNullOrEmpty(SelectedStrategyFilename))
            {
                MessageBox.Show(Application.Current.MainWindow, "No strategy selected", "Cannot run live");
                return;
            }

            if (_runningLive)
            {
                MessageBox.Show(Application.Current.MainWindow, "Strategy already running", "Cannot run live");
                return;
            }

            _runningLive = true;
            Task.Run(() =>
            {
                try
                {
                    RunLive(SelectedStrategyFilename);
                }
                catch (Exception ex)
                {
                    Log.Error("Error running strategy", ex);
                }

                Log.Info("Finished running strategy live");
            });
        }

        private void RunLive(string selectedStrategyFilename)
        {
            // Compile strategy
            var code = File.ReadAllText(Path.Combine(_strategiesDirectory, $"{selectedStrategyFilename}.txt"));
            var strategyType = StrategyHelper.CompileStrategy2(code);

            if (strategyType == null)
            {
                Log.Error("Failed to compile strategy");
                return;
            }

            // Get markets
            var markets = GetStrategyMarkets(strategyType);
        }

        private string[] GetStrategyMarkets(Type strategyType)
        {
            var strategy = (StrategyBase)Activator.CreateInstance(strategyType);
            if (strategy != null && strategy.Markets == null)
            {
                return StrategyBase.Majors.Concat(StrategyBase.Minors).Concat(StrategyBase.MajorIndices).ToArray();
            }

            return strategy.Markets;
        }
    }
}