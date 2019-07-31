using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Broker;
using TraderTools.Core.Services;
using TraderTools.Core.Trading;
using TraderTools.Strategy;

namespace AutomatedTrader.ViewModels
{
    public class StrategyRunLiveViewModel
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
        private IDisposable _strategiesUpdatedDisposable;
        private List<IStrategy> _strategies;
        private FxcmBroker _broker;
        private BrokerAccount _brokerAccount;
        public static string CustomCode { get; set; }

        #endregion

        #region Constructors
        public StrategyRunLiveViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            DependencyContainer.ComposeParts(this);

            Markets = new ObservableCollection<string>(_marketsService.GetMarkets().Select(m => m.Name).OrderBy(x => x));

            RunStrategyCommand = new DelegateCommand(RunStrategyClicked, o => RunStrategyEnabled);
            Strategies = StrategyService.Strategies.ToList();
            _strategiesUpdatedDisposable = StrategyService.UpdatedObservable.Subscribe(StrategiesUpdated);

            _broker = (FxcmBroker)_brokersService.Brokers.First(x => x.Name == "FXCM");
            _brokerAccount = _brokersService.AccountsLookup[_broker];
        }

        private void StrategiesUpdated(object obj)
        {
            Strategies = StrategyService.Strategies.ToList();
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

        #endregion

        #region Properties

        [Import]
        public StrategyService StrategyService { get; private set; }
        public ObservableCollection<string> Markets { get; private set; }
        public DelegateCommand RunStrategyCommand { get; private set; }
        public List<object> SelectedStrategies { get; set; } = new List<object>();
        public List<object> SelectedMarkets { get; set; } = new List<object>();

        public bool RunStrategyEnabled
        {
            get => _runStrategyEnabled;
            private set
            {
                _runStrategyEnabled = value;
                RunStrategyCommand.RaiseCanExecuteChanged();
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

            Task.Run((Action)RunStrategyLive);
        }


        private void NotifyPropertyChanged([CallerMemberName]string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private TimeframeLookupBasicCandleAndIndicators PopulateCandles(IStrategy strategy, string market)
        {
            var maxIndex = 30;
            var ret = new TimeframeLookupBasicCandleAndIndicators();
            var timeframesWithIndicators = strategy.CreateTimeframeIndicators();
            var indicators = IndicatorsHelper.CreateIndicators(strategy.CreateTimeframeIndicators());

            foreach (var timeframeWithIndicators in timeframesWithIndicators)
            {
                if (timeframeWithIndicators.Value == null) continue;

                var candles = _candlesService.GetCandles(_broker, market, timeframeWithIndicators.Key, true, cacheData: false);
                var candlesAndIndicators = new List<BasicCandleAndIndicators>();

                foreach (var candle in candles)
                {
                    var simpleCandle = new SimpleCandle(candle);
                    var basicCandleWithIndicators = new BasicCandleAndIndicators(candle, maxIndex);

                    var timeframeLookupIndex = TimeframeLookup<int>.GetLookupIndex(timeframeWithIndicators.Key);

                    foreach (var indicator in indicators[timeframeLookupIndex])
                    {
                        var value = indicator.Item2.Process(simpleCandle);
                        basicCandleWithIndicators.Set(indicator.Item1, value);
                        candlesAndIndicators.Add(basicCandleWithIndicators);
                    }

                    ret.Add(timeframeWithIndicators.Key, candlesAndIndicators);
                }
            }

            return ret;
        }

        private void RunStrategyLive()
        {
            Log.Info("Running live");
            var strategies = SelectedStrategies.Cast<IStrategy>().ToList();
            var markets = SelectedMarkets.Cast<string>().ToList();

            _dispatcher.Invoke(() => RunStrategyEnabled = false);

            // Update account
            UpdateAccount();

            foreach (var strategy in strategies)
            {
                foreach (var market in markets)
                {
                    // Update candles
                    var candles = PopulateCandles(strategy, market);

                    // Get existing open trades for market
                    var openTrades = _brokerAccount.Trades.Where(t => t.Market == market).ToList();

                    // Create new trades for market
                    var newTrades = strategy.CreateNewTrades(_marketDetailsService.GetMarketDetails("FXCM", market), candles, openTrades);
                    if (newTrades != null && newTrades.Count > 0)
                    {
                        foreach (var trade in newTrades)
                        {
                            if (trade.OrderPrice != null && trade.OrderAmount != null && trade.TradeDirection != null)
                            {
                                if (_broker.CreateOrder(trade.Market, (double)trade.OrderPrice.Value, trade.OrderExpireTime,
                                    trade.OrderAmount.Value, trade.TradeDirection.Value, _candlesService,
                                    _marketDetailsService))
                                {
                                    Log.Info($"Order created for {trade.Market}");
                                }
                                else
                                {
                                    Log.Error($"Unable to create order for {trade.Market}");
                                }
                            }
                        }
                    }
                }
            }

            _dispatcher.Invoke(() => RunStrategyEnabled = true);
            Log.Info("Finished running");


            /*var markets = SelectedMarkets.Cast<string>().ToList();
            _results.Reset();

            _dispatcher.Invoke((Action)(() =>
            {
                _results.RaiseTestRunStarted();

                RunStrategyEnabled = false;
            }));

            var completed = 0;
            var expectedTrades = 0;
            var expectedTradesFound = 0;*/

            /*_producerConsumer = new ProducerConsumer<(IStrategy Strategy, MarketDetails Market)>(3, d =>
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
             _producerConsumer.WaitUntilConsumersFinished();*/

            /*Log.Info($"Found {expectedTrades} - matched {expectedTradesFound}");

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

                RunStrategyEnabled = true;
            }));*/
        }

        private void UpdateAccount()
        {
            _brokerAccount.UpdateBrokerAccount(_broker, _candlesService, _marketDetailsService, _tradeCalculatorService);
            _brokerAccount.SaveAccount(BrokersService.DataDirectory);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}