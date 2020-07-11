using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Services;

namespace StrategyRunnerLive.ViewModels
{
    public class MainWindowViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private FxcmBroker _fxcm;
        private string _strategiesDirectory;

        [Import] private IBrokersService _brokersService;
        [Import] private IDataDirectoryService _dataDirectoryService;

        private string _selectedStrategyFilename;

        public MainWindowViewModel()
        {
            Log.Info("Application started");

            DependencyContainer.ComposeParts(this);

            _strategiesDirectory = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName);

            // Setup brokers
            var brokers = new IBroker[]
            {
                _fxcm = new FxcmBroker
                {
                    IncludeReportInUpdates = false
                }
            };

            _brokersService.AddBrokers(brokers);

            RunStrategyLiveViewModel = new RunStrategyLiveViewModel();

            LoginOutViewModel = new LoginOutViewModel();

            RefreshStrategyFilenames();
        }

        public LoginOutViewModel LoginOutViewModel { get; private set; }

        public RunStrategyLiveViewModel RunStrategyLiveViewModel { get; private set; }

        public string SelectedStrategyFilename
        {
            get => _selectedStrategyFilename;
            set
            {
                _selectedStrategyFilename = value;
                RunStrategyLiveViewModel.SelectedStrategyFilename = _selectedStrategyFilename;
            }
        }

        public ObservableCollection<string> StrategyFilenames { get; } = new ObservableCollection<string>();

        private void RefreshStrategyFilenames()
        {
            StrategyFilenames.Clear();
            var strategyPaths = Directory.GetFiles(_strategiesDirectory, "*.txt");
            foreach (var strategyPath in strategyPaths)
            {
                StrategyFilenames.Add(Path.GetFileNameWithoutExtension(strategyPath));
            }
        }
    }
}