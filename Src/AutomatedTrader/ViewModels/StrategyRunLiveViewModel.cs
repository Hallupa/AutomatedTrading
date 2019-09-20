using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Broker;
using TraderTools.Core.Services;
using TraderTools.Core.Trading;
using TraderTools.Simulation;

namespace AutomatedTrader.ViewModels
{
    public class StrategyRunLiveViewModel
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IBrokersService _brokersService;
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
        private IBrokerAccount _brokerAccount;
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

        /*private TimeframeLookupBasicCandleAndIndicators PopulateCandles(IStrategy strategy, string market)
        {
            Log.Info("Updating candles");
            var maxIndex = 30;
            var ret = new TimeframeLookupBasicCandleAndIndicators();
            var timeframesWithIndicators = strategy.CreateTimeframeIndicators();
            var indicators = IndicatorsHelper.CreateIndicators(strategy.CreateTimeframeIndicators());

            foreach (var timeframeWithIndicators in timeframesWithIndicators)
            {
                if (timeframeWithIndicators.Value == null) continue;

                var candles = _candlesService.GetCandles(_broker, market, timeframeWithIndicators.Key, true, cacheData: false, forceUpdate: true);
                var candlesAndIndicators = new List<CandleAndIndicators>();

                foreach (var candle in candles)
                {
                    var basicCandleWithIndicators = new CandleAndIndicators(candle, maxIndex);

                    var timeframeLookupIndex = TimeframeLookup<int>.GetLookupIndex(timeframeWithIndicators.Key);

                    foreach (var indicator in indicators[timeframeLookupIndex])
                    {
                        var value = indicator.Item2.Process(candle);
                        basicCandleWithIndicators.Set(indicator.Item1, value);
                    }

                    candlesAndIndicators.Add(basicCandleWithIndicators);
                }

                ret.Add(timeframeWithIndicators.Key, candlesAndIndicators);
            }

            return ret;
        }*/

        private void RunStrategyLive()
        {
            Log.Info("Running live");
            var strategies = SelectedStrategies.Cast<IStrategy>().ToList();
            var markets = SelectedMarkets.Cast<string>().ToList();
            var newTradesMarkets = new List<(string Market, string Strategy)>();

            _dispatcher.Invoke(() => RunStrategyEnabled = false);

            var runIntervalMinutes = 5;

            while(true)
            { 
                Log.Info("Running strategies");

                // Update account
                Log.Info("Updating account");
                UpdateAccount(newTradesMarkets);
                newTradesMarkets.Clear();

                Log.Info("Running strategies");

                foreach (var strategy in strategies)
                {
                    foreach (var market in markets)
                    {
                        Log.Info($"Running for strategy: {strategy.Name} market: {market}");

                        // Update candles
                        var candles = SimulationRunner.PopulateCandles(_broker, strategy, market, _candlesService);

                        // Get existing open trades for market
                        var previousTrades = _brokerAccount.Trades.Where(t => t.Market == market).ToList();

                        // Create new trades for market
                        var newTrades = strategy.CreateNewTrades(_marketDetailsService.GetMarketDetails("FXCM", market),
                            candles, previousTrades, _tradeCalculatorService);
                        if (newTrades != null && newTrades.Count > 0)
                        {
                            foreach (var trade in newTrades)
                            {
                                trade.Strategies = strategy.Name;

                                if (trade.OrderPrice != null && trade.OrderAmount != null && trade.TradeDirection != null)
                                {
                                    if (_broker.CreateOrder(trade.Market, (double) trade.OrderPrice.Value,
                                        trade.StopPrice != null ? (double?) trade.StopPrice.Value : null,
                                        trade.LimitPrice != null ? (double?) trade.LimitPrice.Value : null,
                                        trade.OrderExpireTime,
                                        trade.OrderAmount.Value, trade.TradeDirection.Value, _candlesService,
                                        _marketDetailsService))
                                    {
                                        newTradesMarkets.Add((trade.Market, strategy.Name));
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

                Log.Info("Running strategies completed - waiting for next run");

                Thread.Sleep(1000 * 60 * runIntervalMinutes);
            }

            _dispatcher.Invoke(() => RunStrategyEnabled = true);
            Log.Info("Finished running");
        }

        private void UpdateAccount(List<(string Market, string Strategy)> newTrades = null)
        {
            _brokerAccount.UpdateBrokerAccount(_broker, _candlesService, _marketDetailsService, _tradeCalculatorService, UpdateOption.ForceUpdate);

            var orderedTrades = _brokerAccount.Trades.OrderByDescending(t => t.Id).ToList();

            foreach (var newTrade in newTrades)
            {
                var trade = orderedTrades.FirstOrDefault(t => t.Market == newTrade.Market);
                if (trade != null)
                {
                    trade.Strategies = newTrade.Strategy;
                }
            }

            _brokerAccount.SaveAccount();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}