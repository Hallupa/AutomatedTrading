using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Hallupa.Library;
using Hallupa.TraderTools.Brokers.Binance;
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
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IMarketDetailsService _marketDetailsService;
        [Import] private ITradeDetailsAutoCalculatorService _tradeDetailsAutoCalculatorService;

        private string _selectedStrategyFilename;

        public MainWindowViewModel()
        {
            Log.Info("Application started");

            try
            {
                DependencyContainer.ComposeParts(this);

                _strategiesDirectory = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName);

                if (!Directory.Exists(_strategiesDirectory))
                {
                    Directory.CreateDirectory(_strategiesDirectory);
                }

                var binance = new BinanceBroker();

                // Setup brokers
                var brokers = new IBroker[]
                {
                    _fxcm = new FxcmBroker
                    {
                        IncludeReportInUpdates = false
                    },
                    binance
                };

                _brokersService.AddBrokers(brokers);

                var logDirectory = DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog");
                _brokersService.LoadBrokerAccounts(_tradeDetailsAutoCalculatorService, logDirectory);

                var account = _brokersService.AccountsLookup[binance];

                Task.Run(() =>
                {
                    // TODO - threading
                    /*account.UpdateBrokerAccount(
                        binance,
                        _candlesService,
                        _marketDetailsService,
                        _tradeDetailsAutoCalculatorService);
                    account.SaveAccount(DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog"));*/

                });

                RunStrategyLiveViewModel = new RunStrategyLiveViewModel();

                LoginOutViewModel = new LoginOutViewModel();

                RefreshStrategyFilenames();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to create MainWindowViewModel", ex);
                throw;
            }
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
            var strategyPaths = Directory.GetFiles(_strategiesDirectory, "*.cs");
            foreach (var strategyPath in strategyPaths)
            {
                StrategyFilenames.Add(Path.GetFileNameWithoutExtension(strategyPath));
            }
        }
    }
}