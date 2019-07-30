using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Core.Services;
using TraderTools.Core.Trading;
using TraderTools.Strategy;

namespace AutomatedTraderDesigner.ViewModels
{
    public class StrategyRunViewModel : INotifyPropertyChanged
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private BrokersService _brokersService;
        [Import] private StrategyRunnerResultsService _results;
        [Import] private MarketsService _marketsService;
        [Import] private StrategyService _strategyService;
        [Import] private IMarketDetailsService _marketDetailsService;
        [Import] private ITradeDetailsAutoCalculatorService _tradeCalculatorService;
        private bool _runStrategyEnabled = true;
        private Dispatcher _dispatcher;
        private ProducerConsumer<(IStrategy Strategy, MarketDetails Market)> _producerConsumer;
        private bool _saveCacheEnabled = true;
        private IDisposable _strategiesUpdatedDisposable;
        private List<IStrategy> _strategies;
        public static string CustomCode { get; set; }

        #endregion

        #region Constructors

        public StrategyRunViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            DependencyContainer.ComposeParts(this);

            _tradeCalculatorService.SetOptions(CalculateOptions.ExcludePricePerPip);

            Markets = new ObservableCollection<string>(_marketsService.GetMarkets().Select(m => m.Name).OrderBy(x => x));

            // Load existing results
            var savedResultsPath = Path.Combine(BrokersService.DataDirectory, @"StrategyTester\StrategyTesterResults.json");
            if (File.Exists(savedResultsPath))
            {
                var results = JsonConvert.DeserializeObject<List<Trade>>(File.ReadAllText(savedResultsPath));
                _results.AddResult(results);
                _results.RaiseTestRunCompleted();
            }

            RunStrategyCommand = new DelegateCommand(RunStrategyClicked);
            SaveCacheCommand = new DelegateCommand(o => SaveCache(), o => _saveCacheEnabled);
            LoadCacheCommand = new DelegateCommand(o => LoadCache());
            ClearCachedTradesCommand = new DelegateCommand(o => ClearCachedTrades());
            Strategies = StrategyService.Strategies.ToList();
            _strategiesUpdatedDisposable = StrategyService.UpdatedObservable.Subscribe(StrategiesUpdated);
        }

        private void StrategiesUpdated(object obj)
        {
            Strategies = StrategyService.Strategies.ToList();
        }

        private void ClearCachedTrades()
        {
            StrategyRunner.Cache.TradesLookup.Clear();
            GC.Collect();
        }

        public List<IStrategy> Strategies
        {
            get => _strategies;
            private set
            {
                _strategies = value;
                OnPropertyChanged();
            }
        }

        private void SaveCache()
        {
            /*_saveCacheEnabled = false;
            SaveCacheCommand.RaiseCanExecuteChanged();
            Log.Info("Saving cache");

            Task.Run(() =>
            {
                // Save candles
                var path = "";
                using (var memoryStream = new MemoryStream())
                using (var compressionStream = new GZipStream(memoryStream, CompressionMode.Compress))
                using (var sw = new BinaryWriter(compressionStream))
                {
                    foreach (var marketTimeframeAndCandles in StrategyRunner.Cache.CandlesLookup)
                    {
                        sw.Write(marketTimeframeAndCandles.Key);

                        foreach (var timeframeAndCandles in marketTimeframeAndCandles.Value)
                        {
                            sw.Write((int)timeframeAndCandles.Key);
                            var count = timeframeAndCandles.Value.Count;
                            sw.Write(count);

                            for (var i = 0; i < count; i++)
                            {
                                timeframeAndCandles.Value[i].Serialise(sw);
                            }
                        }
                    }
                }

                var cacheTmpPath = Path.Combine(BrokersService.DataDirectory, "StrategyTester\\StrategyTesterCache_tmp.dat");
                var cacheFinalPath = Path.Combine(BrokersService.DataDirectory, "StrategyTester\\StrategyTesterCache.dat");

                if (File.Exists(cacheTmpPath))
                {
                    File.Delete(cacheTmpPath);
                }

                File.WriteAllBytes(cacheTmpPath, ZeroFormatterSerializer.Serialize(StrategyRunner.Cache));

                if (File.Exists(cacheFinalPath))
                {
                    File.Delete(cacheFinalPath);
                }

                File.Move(cacheTmpPath, cacheFinalPath);

                _dispatcher.Invoke(() =>
                {
                    _saveCacheEnabled = true;
                    SaveCacheCommand.RaiseCanExecuteChanged();
                });

                Log.Info("Saved cache");
            });*/
        }

        private void LoadCache()
        {
        }

        #endregion

        #region Properties

        [Import]
        public StrategyService StrategyService { get; private set; }
        public ObservableCollection<string> Markets { get; private set; }
        public ICommand RunStrategyCommand { get; private set; }
        public List<object> SelectedStrategies { get; set; } = new List<object>();
        public List<object> SelectedMarkets { get; set; } = new List<object>();
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public DelegateCommand SaveCacheCommand { get; private set; }
        public DelegateCommand LoadCacheCommand { get; private set; }
        public DelegateCommand ClearCachedTradesCommand { get; private set; }
        public bool UpdatePrices { get; set; }


        public bool RunStrategyEnabled
        {
            get { return _runStrategyEnabled; }
            private set
            {
                _runStrategyEnabled = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        private void RunStrategyClicked(object o)
        {
            if (SelectedMarkets.Count == 0 || SelectedStrategies.Count == 0)
            {
                return;
            }

            RunStrategyEnabled = false;

            Task.Run((Action)RunStrategy);
        }


        private void NotifyPropertyChanged([CallerMemberName]string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void RunStrategy()
        {
            Log.Info("Running simulation");
            var stopwatch = Stopwatch.StartNew();
            var strategies = SelectedStrategies.ToList();
            var broker = _brokersService.Brokers.First(x => x.Name == "FXCM");

            var markets = SelectedMarkets.Cast<string>().ToList();
            _results.Reset();

            _dispatcher.Invoke((Action)(() =>
            {
                _results.RaiseTestRunStarted();

                NotifyPropertyChanged("TotalTrades");
                NotifyPropertyChanged("AverageRTrade");
                NotifyPropertyChanged("TotalR");
                NotifyPropertyChanged("PercentSuccessTrades");
                NotifyPropertyChanged("AverageWinningRRRTrades");
                NotifyPropertyChanged("AverageLosingRRRTrades");

                RunStrategyEnabled = false;
            }));

            var completed = 0;
            var expectedTrades = 0;
            var expectedTradesFound = 0;

            _producerConsumer = new ProducerConsumer<(IStrategy Strategy, MarketDetails Market)>(3, d =>
            {
                var strategyTester = new StrategyRunner(_candlesService, _tradeCalculatorService);
                var earliest = !string.IsNullOrEmpty(StartDate) ? (DateTime?)DateTime.Parse(StartDate) : null;
                var latest = !string.IsNullOrEmpty(EndDate) ? (DateTime?)DateTime.Parse(EndDate) : null;
                var result = strategyTester.Run(d.Strategy, d.Market, broker,
                    out var expegtedTradesForMarket, out var expectedTradesForMarketFound,
                    earliest, latest, updatePrices: UpdatePrices);

                Interlocked.Add(ref expectedTrades, expegtedTradesForMarket);
                Interlocked.Add(ref expectedTradesFound, expectedTradesForMarketFound);

                if (result != null)
                {
                    _results.AddResult(result);

                    // Adding trades to UI in realtime slows down the UI too much with strategies with many trades

                    completed++;
                    Log.Info($"Completed {completed}/{markets.Count * strategies.Count}");
                }

                return ProducerConsumerActionResult.Success;
            });

            foreach (var market in markets)
            {
                foreach (var strategy in strategies.Cast<IStrategy>())
                {
                    _producerConsumer.Add((strategy, _marketDetailsService.GetMarketDetails(broker.Name, market)));
                }
            }

            _producerConsumer.Start();
            _producerConsumer.SetProducerCompleted();
            _producerConsumer.WaitUntilConsumersFinished();

            stopwatch.Stop();
            Log.Info($"Simulation run completed in {stopwatch.Elapsed.TotalSeconds}s");
            Log.Info($"Found {expectedTrades} - matched {expectedTradesFound}");

            // Save results
            var savedResulsPath = Path.Combine(BrokersService.DataDirectory, @"StrategyTester\StrategyTesterResults.json");
            if (File.Exists(savedResulsPath))
            {
                File.Delete(savedResulsPath);
            }
            File.WriteAllText(savedResulsPath, JsonConvert.SerializeObject(_results.Results));

            _dispatcher.Invoke((Action)(() =>
            {
                _results.RaiseTestRunCompleted();

                NotifyPropertyChanged("TotalTrades");
                NotifyPropertyChanged("AverageRTrade");
                NotifyPropertyChanged("TotalR");
                NotifyPropertyChanged("PercentSuccessTrades");
                NotifyPropertyChanged("AverageWinningRRRTrades");
                NotifyPropertyChanged("AverageLosingRRRTrades");

                RunStrategyEnabled = true;
            }));
        }

        public void ViewClosing()
        {
            _producerConsumer?.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}