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
using TraderTools.Basics.Extensions;
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
            var trades = new TradeWithIndexingCollection();
            var strategyLookup = new Dictionary<string, StrategyBase>();
            var candlesLookup = new Dictionary<string, TimeframeLookup<List<Candle>>>();
            var accountSaveIntervalSeconds = 60;
            var accountLastSaveTime = DateTime.UtcNow;

            Log.Info("Running live");

            // Get strategy type and markets
            var strategyType = CompileStrategyAndGetStrategyMarkets(selectedStrategyFilename, out var markets, out var timeframes);
            if (strategyType == null) return;

            // Update broker account
            Log.Info("Updating broker account");
            _brokerAccount.UpdateBrokerAccount(_fxcm, _candlesService, _marketDetailsService, _tradeDetailsAutoCalculatorService, UpdateOption.ForceUpdate);

            // Get candles
            Log.Info("Getting candles");
            foreach (var m in markets)
            {
                candlesLookup[m] = new TimeframeLookup<List<Candle>>();

                foreach (var t in timeframes)
                {
                    candlesLookup[m].Add(t, _candlesService.GetCandles(_fxcm, m, t, true, forceUpdate: true, cacheData: true));
                }
            }

            // Setup locks
            foreach (var market in markets)
            {
                _marketLock[market] = new object();
            }

            // Create strategies
            Log.Info("Setting up strategies");
            foreach (var market in markets)
            {
                var strategy = (StrategyBase)Activator.CreateInstance(strategyType);
                var currentCandles = new TimeframeLookup<List<Candle>>();
                strategy.SetSimulationParameters(trades, currentCandles, _marketDetailsService.GetMarketDetails("FXCM", market));
                strategyLookup.Add(market, strategy);

                // Setup candles for strategy
                foreach (var t in timeframes)
                {
                    currentCandles.Add(t, candlesLookup[market][t].Where(c => c.IsComplete == 1).ToList());
                }
            }

            // Get live prices steams
            var priceMonitor = new MonitorLivePrices(_fxcm, p => ProcessNewPrice(markets, timeframes, p, candlesLookup));

            try
            {
                var checkFxcmConnectedIntervalSeconds = 60 * 5;
                var nextFxcmConnectedCheckTime = DateTime.UtcNow.AddSeconds(checkFxcmConnectedIntervalSeconds);

                Log.Info("Running main processing loop");
                while (true)
                {
                    if (accountLastSaveTime < DateTime.UtcNow.AddSeconds(-accountSaveIntervalSeconds))
                    {
                        lock (_brokerAccount)
                        {
                            Log.Debug("Saving broker account");
                            _brokerAccount.SaveAccount(
                                DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog"));
                        }

                        accountLastSaveTime = DateTime.UtcNow;
                    }

                    // Re-connect if connect is lost
                    if (DateTime.UtcNow >= nextFxcmConnectedCheckTime)
                    {
                        nextFxcmConnectedCheckTime = DateTime.UtcNow.AddSeconds(checkFxcmConnectedIntervalSeconds);
                        if (_fxcm.Status == ConnectStatus.Disconnected)
                        {
                            Log.Warn("FXCM has disconnected - reconnecting");
                            try
                            {
                                priceMonitor?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Unable to dispose price monitor", ex);
                            }

                            priceMonitor = null;

                            _fxcm.Connect();

                            if (_fxcm.Status == ConnectStatus.Connected)
                            {
                                Log.Warn($"FXCM has reconnected");
                                priceMonitor = new MonitorLivePrices(_fxcm, p => ProcessNewPrice(markets, timeframes, p, candlesLookup));
                            }
                            else
                            {
                                Log.Warn($"FXCM hasn't re-connected - new status is: {_fxcm.Status}");
                            }
                        }
                    }

                    foreach (var strategy in strategyLookup.Values)
                    {
                        var newTimeframeCandles = new List<Timeframe>();

                        // Check if there is any new complete candles
                        foreach (var t in strategy.Timeframes)
                        {
                            lock (candlesLookup[strategy.Market.Name][t])
                            {
                                if (strategy.Candles[t].Count !=
                                    candlesLookup[strategy.Market.Name][t].Count(c => c.IsComplete == 1))
                                {
                                    newTimeframeCandles.Add(t);
                                    strategy.Candles[t].Clear();
                                    strategy.Candles[t].AddRange(candlesLookup[strategy.Market.Name][t]
                                        .Where(c => c.IsComplete == 1).ToList());
                                }
                            }
                        }

                        if (newTimeframeCandles.Any()) // TODO reduce times this is called and include save
                        {
                            // Update broker account
                            lock (_brokerAccount)
                            {
                                Log.Debug("Updating and saving broker account");
                                _brokerAccount.UpdateBrokerAccount(_fxcm, _candlesService, _marketDetailsService,
                                    _tradeDetailsAutoCalculatorService, UpdateOption.ForceUpdate);
                            }

                            var s = strategy;
                            Task.Run(() =>
                            {
                                if (Monitor.TryEnter(_marketLock[s.Market.Name]))
                                {
                                    try
                                    {
                                        Log.Info($"Found new candles for market: {s.Market.Name}");

                                        // Update indicators and do trades maintenance
                                        s.UpdateIndicators(newTimeframeCandles);
                                        s.NewTrades.Clear();
                                        s.Trades.MoveTrades();

                                        var beforeStopLossLookup =
                                            s.Trades.OpenTrades.ToDictionary(x => x.Trade.Id, x => x.Trade.StopPrice);

                                        // Process strategy
                                        s.ProcessCandles(newTimeframeCandles);

                                        // Create any new trades
                                        CreateNewFXCMTradesAndUpdateAccount(s);

                                        if (trades.OpenTrades.Count() > 5)
                                        {
                                            Log.Error("Too many trades");
                                        }

                                        // Update any stops
                                        UpdateFXCMOpenTradesStops(s, beforeStopLossLookup);

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

            }
            finally
            {
                priceMonitor.Dispose();
            } }

        private static void ProcessNewPrice(string[] markets, Timeframe[] timeframes,
            (string Instrument, double Bid, double Ask, DateTime Time) p, Dictionary<string, TimeframeLookup<List<Candle>>> candles)
        {
            if (markets.Contains(p.Instrument))
            {
                foreach (var timeframe in timeframes)
                {
                    lock (candles[p.Instrument][timeframe])
                    {
                        var c = candles[p.Instrument][timeframe].Last();
                        var updated = false;

                        // Update existing candle
                        if (p.Time.Ticks <= c.CloseTimeTicks && p.Time.Ticks >= c.OpenTimeTicks)
                        {
                            if (p.Ask > c.HighAsk) c.HighAsk = (float)p.Ask;
                            if (p.Ask < c.LowAsk) c.LowAsk = (float)p.Ask;
                            if (p.Bid > c.HighBid) c.HighBid = (float)p.Bid;
                            if (p.Bid < c.LowBid) c.LowBid = (float)p.Bid;

                            c.CloseAsk = (float)p.Ask;
                            c.CloseBid = (float)p.Bid;
                            updated = true;
                        }

                        // If time is after current candle - set complete
                        if (p.Time.Ticks >= c.CloseTimeTicks)
                        {
                            Log.Info($"Market {p.Instrument} has complete {timeframe} candle");
                            c.IsComplete = 1;
                            updated = true;
                        }

                        if (updated) candles[p.Instrument][timeframe][candles[p.Instrument][timeframe].Count - 1] = c;

                        // Add new candle if required
                        if (p.Time.Ticks >= c.CloseTimeTicks)
                        {
                            var targetStartTime = c.CloseTime();
                            var targetEndTime = targetStartTime.AddSeconds((int)timeframe);

                            while (true)
                            {
                                if (p.Time.Ticks >= targetStartTime.Ticks && p.Time.Ticks <= targetEndTime.Ticks)
                                {
                                    candles[p.Instrument][timeframe].Add(new Candle
                                    {
                                        OpenAsk = (float)p.Ask,
                                        CloseAsk = (float)p.Ask,
                                        OpenBid = (float)p.Bid,
                                        CloseBid = (float)p.Bid,
                                        CloseTimeTicks = targetEndTime.Ticks,
                                        OpenTimeTicks = targetStartTime.Ticks,
                                        IsComplete = 0,
                                        LowBid = (float)p.Bid,
                                        HighAsk = (float)p.Ask,
                                        HighBid = (float)p.Bid,
                                        LowAsk = (float)p.Ask
                                    });

                                    break;
                                }

                                targetEndTime = targetEndTime.AddSeconds((int)timeframe);
                                targetStartTime = targetStartTime.AddSeconds((int)timeframe);
                            }
                        }
                    }
                }
            }
        }

        private Type CompileStrategyAndGetStrategyMarkets(string selectedStrategyFilename, out string[] markets, out Timeframe[] timeframes)
        {
            markets = null;
            timeframes = null;

            // Compile strategy
            var code = File.ReadAllText(Path.Combine(_strategiesDirectory, $"{selectedStrategyFilename}.txt"));
            var strategyType = StrategyHelper.CompileStrategy(code);

            if (strategyType == null)
            {
                Log.Error("Failed to compile strategy");
                return strategyType;
            }

            // Get markets
            markets = StrategyHelper.GetStrategyMarkets(strategyType);
            timeframes = GetStrategyTimeframes(strategyType);
            return strategyType;
        }


        private void UpdateFXCMOpenTradesStops(StrategyBase strategy, Dictionary<string, decimal?> beforeStopLossLookup)
        {
            foreach (var t in strategy.Trades.OpenTrades.Where(x => strategy.NewTrades.All(z => z.Id != x.Trade.Id)))
            {
                if (t.Trade.StopPrice != beforeStopLossLookup[t.Trade.Id])
                {
                    if (!_fxcm.ChangeStop(t.Trade.StopPrices.Last(x => !string.IsNullOrEmpty(x.Id)).Id,
                        (double)t.Trade.StopPrice.Value))
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
                    (double)trade.StopPrice))
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

        private Timeframe[] GetStrategyTimeframes(Type strategyType)
        {
            var strategy = (StrategyBase)Activator.CreateInstance(strategyType);
            return strategy.Timeframes;
        }
    }
}