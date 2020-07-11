using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Services;
using TraderTools.Simulation;

namespace StrategyRunnerLive.ViewModels
{
    public class RunStrategyLiveViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private FxcmBroker _fxcm;
        private bool _runningLive = false;
        private string _strategiesDirectory;
        private string _logDirectory;
        private IBrokerAccount _brokerAccount;

        [Import] private IBrokersService _brokersService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        [Import] private ITradeDetailsAutoCalculatorService _tradeDetailsAutoCalculatorService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IMarketDetailsService _marketDetailsService;

        public RunStrategyLiveViewModel()
        {
            DependencyContainer.ComposeParts(this);

            RunLiveCommand = new DelegateCommand(o => RunLive());
            _fxcm = (FxcmBroker)_brokersService.Brokers.First(x => x.Name == "FXCM");
            _strategiesDirectory = _dataDirectoryService.MainDirectoryWithApplicationName;
            _logDirectory = DataDirectoryService.GetMainDirectoryWithApplicationName("FXCMTradeLog");
            _brokersService.LoadBrokerAccounts(_tradeDetailsAutoCalculatorService, _logDirectory);
            _brokerAccount = _brokersService.AccountsLookup[_fxcm];
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

        private Dictionary<string, object> _marketLock = new Dictionary<string, object>();

        private void RunLive(string selectedStrategyFilename)
        {
            Log.Info("Running live");

            var strategyType = CompileStrategyAndGetStrategyMarkets(selectedStrategyFilename, out var markets);
            if (strategyType == null) return;

            // Update broker account
            _brokerAccount.UpdateBrokerAccount(_fxcm, _candlesService, _marketDetailsService, _tradeDetailsAutoCalculatorService, UpdateOption.ForceUpdate);

            var trades = new TradeWithIndexingCollection();
            var strategyLookup = new Dictionary<string, StrategyBase>();

            // Setup strategies and candles
            SetupMarketStrategiesWithCandles(markets, strategyType, trades, strategyLookup);

            // Get live prices steams
            var candlesLastProcessed = new TimeframeLookup<List<Candle>>();
            foreach (var s in strategyLookup.Values)
            {
                _marketLock[s.Market.Name] = new object();

                foreach (var t in s.Timeframes)
                {
                    candlesLastProcessed[t] ??= new List<Candle>();
                    candlesLastProcessed[t].Clear();
                    candlesLastProcessed[t].AddRange(s.Candles[t]);
                }
            }

            using var priceMonitor = new MonitorLivePrices(_fxcm, p =>
            {
                Task.Run(() => ProcessNewPrice(markets, p, strategyLookup));
            });

            var accountSaveIntervalSeconds = 60;
            var accountLastSaveTime = DateTime.UtcNow;

            while (true)
            {
                Log.Debug("Updating broker account");
                _brokerAccount.UpdateBrokerAccount(_fxcm, _candlesService, _marketDetailsService, _tradeDetailsAutoCalculatorService, UpdateOption.ForceUpdate);
                if (accountLastSaveTime < DateTime.UtcNow.AddSeconds(-accountSaveIntervalSeconds))
                {
                    _brokerAccount.SaveAccount(DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog"));
                }

                foreach (var strategy in strategyLookup.Values)
                {
                    var newTimeframeCandles = new List<Timeframe>();
                    foreach (var t in strategy.Timeframes)
                    {
                        if (strategy.Candles[t].Count != candlesLastProcessed[t].Count)
                        {
                            newTimeframeCandles.Add(t);
                        }
                    }

                    if (newTimeframeCandles.Any())
                    {
                        var s = strategy;
                        Task.Run(() =>
                        {
                            if (Monitor.TryEnter(_marketLock[s.Market.Name]))
                            {
                                try
                                {
                                    Log.Debug($"Found new candles for market: {s.Market.Name}");
                                    s.UpdateIndicators(newTimeframeCandles);
                                    s.NewTrades.Clear();
                                    s.Trades.MoveTrades();

                                    var beforeStopLossLookup =
                                        s.Trades.OpenTrades.ToDictionary(x => x.Trade.Id, x => x.Trade.StopPrice);
                                    s.ProcessCandles(newTimeframeCandles);

                                    CreateNewFXCMTradesAndUpdateAccount(s);

                                    UpdateFXCMOpenTradesStops(s, beforeStopLossLookup);

                                    SetStrategyCompletedCandles(s);
                                }
                                finally
                                {
                                    Monitor.Exit(_marketLock[s.Market.Name]);
                                }
                            }
                        });
                    }
                }

                Thread.Sleep(100);
            }




            /*int lastMinute = -1;
            while (true)
            {
                Log.Info($"Checking for updates - {DateTime.UtcNow}");

                Log.Debug("Updating broker account");
                _brokerAccount.UpdateBrokerAccount(_fxcm, _candlesService, _marketDetailsService, _tradeDetailsAutoCalculatorService, UpdateOption.ForceUpdate);
                _brokerAccount.SaveAccount(DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog"));

                var producerConsumer = new ProducerConsumer<string>(3, market =>
                {
                    ProcessMarket(strategyLookup[market], market);
                    return ProducerConsumerActionResult.Success;
                });

                foreach (var market in markets)
                {
                    producerConsumer.Add(market);
                }

                producerConsumer.SetProducerCompleted();
                producerConsumer.Start();
                producerConsumer.WaitUntilConsumersFinished();

                Log.Info("Check complete");


                var nextRun = false;
                while (!nextRun)
                {
                    Thread.Sleep(250);

                    var utcNow = DateTime.UtcNow;
                    nextRun = utcNow.Minute != lastMinute && utcNow.Minute % 5 == 0 && utcNow.Second > 10;

                    if (nextRun) lastMinute = utcNow.Minute;
                }
            }*/
        }

        private static void ProcessNewPrice(string[] markets, (string Instrument, double Bid, double Ask, DateTime Time) p, Dictionary<string, StrategyBase> strategyLookup)
        {
            // TODO Run in thread
            if (markets.Contains(p.Instrument))
            {
                var strategy = strategyLookup[p.Instrument];

                foreach (var timeframe in strategy.Timeframes)
                {
                    var c = strategy.Candles[timeframe].Last();

                    // Update existing incomplete candle
                    if (c.CloseTimeTicks >= p.Time.Ticks
                        && c.OpenTimeTicks <= p.Time.Ticks
                        && c.IsComplete == 0)
                    {
                        if (p.Ask > c.HighAsk) c.HighAsk = (float) p.Ask;
                        if (p.Ask < c.LowAsk) c.LowAsk = (float) p.Ask;
                        if (p.Bid > c.HighBid) c.HighBid = (float) p.Bid;
                        if (p.Bid < c.LowBid) c.LowBid = (float) p.Bid;

                        c.CloseAsk = (float) p.Ask;
                        c.CloseBid = (float) p.Bid;
                    }

                    if (c.CloseTimeTicks <= p.Time.Ticks
                        && c.IsComplete == 0)
                    {
                        c.IsComplete = 1;
                    }
                }

                // TODO If new complete candles, process

                // TODO If new complete candles, update candles. Check new candles are received
            }
        }

        private void SetupMarketStrategiesWithCandles(string[] markets, Type strategyType, TradeWithIndexingCollection trades,
            Dictionary<string, StrategyBase> strategyLookup)
        {
            foreach (var market in markets)
            {
                var strategy = (StrategyBase) Activator.CreateInstance(strategyType);
                var currentCandles = new TimeframeLookup<List<Candle>>();

                strategy.SetSimulationParameters(trades, currentCandles,
                    _marketDetailsService.GetMarketDetails("FXCM", market));
                strategyLookup.Add(market, strategy);

                SetStrategyCompletedCandles(strategy);
            }
        }

        private Type CompileStrategyAndGetStrategyMarkets(string selectedStrategyFilename, out string[] markets)
        {
            markets = null;

            // Compile strategy
            var code = File.ReadAllText(Path.Combine(_strategiesDirectory, $"{selectedStrategyFilename}.txt"));
            var strategyType = StrategyHelper.CompileStrategy2(code);

            if (strategyType == null)
            {
                Log.Error("Failed to compile strategy");
                return strategyType;
            }

            // Get markets
            markets = GetStrategyMarkets(strategyType);
            return strategyType;
        }

        /*private void ProcessMarket(StrategyBase strategy, string market)
        {
            Log.Debug($"Checking market: {market}");
            strategy.Trades.MoveTrades();

            TODO // need some changes
            SetStrategyCompletedCandles(strategy, out var newCandleTimesframes, out var newCandles);

            if (newCandleTimesframes.Any())
            {
                Log.Debug($"Found new candles for market: {market}");
                strategy.UpdateIndicators(newCandleTimesframes);
                strategy.NewTrades.Clear();

                // Only process candles if they closed recently
                var timeSinceClosedMs = newCandles
                    .Select(c => Math.Abs((DateTime.UtcNow - new DateTime(c.CloseTimeTicks)).TotalMilliseconds)).OrderBy(x => x)
                    .First();
                if (timeSinceClosedMs < 35000)
                {
                    var beforeStopLossLookup =
                        strategy.Trades.OpenTrades.ToDictionary(x => x.Trade.Id, x => x.Trade.StopPrice);
                    strategy.ProcessCandles(newCandleTimesframes);

                    CreateNewFXCMTradesAndUpdateAccount(strategy);

                    UpdateFXCMOpenTradesStops(strategy, beforeStopLossLookup);
                }
                else
                {
                    Log.Warn($"Candles closed not recently enough to be processed ({timeSinceClosedMs}ms)");
                }
            }
        }*/

        private void UpdateFXCMOpenTradesStops(StrategyBase strategy, Dictionary<string, decimal?> beforeStopLossLookup)
        {
            foreach (var t in strategy.Trades.OpenTrades.Where(x => strategy.NewTrades.All(z => z.Id != x.Trade.Id)))
            {
                if (t.Trade.StopPrice != beforeStopLossLookup[t.Trade.Id])
                {
                    if (!_fxcm.ChangeStop(t.Trade.StopPrices.Last(x => !string.IsNullOrEmpty(x.Id)).Id,
                        (double) t.Trade.StopPrice.Value))
                    {
                        Log.Error($"Unable to change stop price for trade Id: {t.Trade.Id}");
                    }
                }
            }
        }

        private void CreateNewFXCMTradesAndUpdateAccount(StrategyBase strategy)
        {
            var newOrderIds = new List<string>();
            foreach (var trade in strategy.NewTrades.Where(t => t.EntryPrice != null))
            {
                if (_fxcm.CreateMarketOrder(trade.Market, 1, trade.TradeDirection.Value, out var orderId,
                    (double) trade.StopPrice))
                {
                    newOrderIds.Add(orderId);
                }
            }

            if (newOrderIds.Any())
            {
                _brokerAccount.UpdateBrokerAccount(_fxcm, _candlesService, _marketDetailsService, _tradeDetailsAutoCalculatorService, UpdateOption.ForceUpdate);

                foreach (var orderId in newOrderIds)
                {
                    var newTrade = _brokerAccount.Trades.FirstOrDefault(t => t.OrderId == orderId);
                    if (newTrade != null)
                    {
                        newTrade.Strategies = SelectedStrategyFilename;
                        newTrade.Comments = "Created by auto trader";
                    }

                    _brokerAccount.SaveAccount(DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog"));
                    strategy.Trades.AddTrade(newTrade);
                }
            }
        }

        private void SetStrategyCompletedCandles(StrategyBase strategy)
        {
            foreach (var timeframe in strategy.Timeframes)
            {
                var candles = _candlesService.GetCandles(_fxcm, strategy.Market.Name, timeframe, true, forceUpdate: true, cacheData: true)
                    .Where(c => c.IsComplete == 1).ToList();

                strategy.Candles[timeframe] ??= new List<Candle>();
                strategy.Candles[timeframe].AddRange(candles);
            }
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