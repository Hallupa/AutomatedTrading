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
using Hallupa.TraderTools.Brokers.Binance;
using Hallupa.TraderTools.Simulation;
using log4net;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Services;
using TraderTools.Simulation;

namespace StrategyRunnerLive.ViewModels
{
    public class RunStrategyLiveViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool _runningLive = false;
        private string _strategiesDirectory;
        private string _logDirectory;

        [Import] private IBrokersService _brokersService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        [Import] private ITradeDetailsAutoCalculatorService _tradeDetailsAutoCalculatorService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IMarketDetailsService _marketDetailsService;

        public RunStrategyLiveViewModel()
        {
            DependencyContainer.ComposeParts(this);

            RunLiveCommand = new DelegateCommand(o => RunLive());
            _strategiesDirectory = _dataDirectoryService.MainDirectoryWithApplicationName;
            _logDirectory = DataDirectoryService.GetMainDirectoryWithApplicationName("TradeLog");
            _brokersService.LoadBrokerAccounts(_tradeDetailsAutoCalculatorService, _logDirectory);
        }

        public DelegateCommand RunLiveCommand { get; }

        public string SelectedStrategyFilename { get; set; }

        private void RunLive()
        {
            /*if (_fxcm.Status != ConnectStatus.Connected)
            {
                MessageBox.Show(Application.Current.MainWindow, "FXCM not connected", "Cannot run live");
                return;
            }*/

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
            var trades = new TradeWithIndexingCollection();
            var candlesLookup = new Dictionary<string, TimeframeLookup<List<Candle>>>();
            var accountSaveIntervalSeconds = 60;

            Log.Info("Running live");

            // Get strategy type and markets
            var strategyType = CompileStrategyAndGetStrategyMarkets(selectedStrategyFilename);
            if (strategyType == null) return;

            var strategy = (StrategyBase)Activator.CreateInstance(strategyType);

            // Get broker and broker account
            var broker = (BinanceBroker)_brokersService.Brokers.First(x => x.Name == strategy.Broker);


            // Get candles
            Log.Info("Getting candles");
            foreach (var m in strategy.Markets)
            {
                candlesLookup[m] = new TimeframeLookup<List<Candle>>();

                foreach (var t in strategy.Timeframes)
                {
                    candlesLookup[m].Add(t,
                        _candlesService.GetCandles(broker, m, t, true, forceUpdate: true, cacheData: true)
                            .Where(c => c.IsComplete == 1)
                            .ToList());
                }
            }

            // Create strategies
            strategy.IsLive = true;
            strategy.SetSimulationParameters(trades, candlesLookup);
            strategy.SetInitialised(
                true,
                () => broker.GetBalance(),
                (indexing, trade, arg3) => { },
                broker, 
                _brokersService);

            // Update indicators
            var a = new List<AddedCandleTimeframe>();
            foreach (var m in strategy.Markets)
            {
                foreach (var t in strategy.Timeframes)
                {
                    var candles = _candlesService.GetCandles(broker, m, t, true, forceUpdate: false)
                        .Where(c => c.IsComplete == 1)
                        .ToList();

                    foreach (var c in candles)
                    {
                        a.Add(new AddedCandleTimeframe(m, t, c));
                    }
                }
            }
            strategy.UpdateIndicators(a);

            strategy.UpdateBalances();
            strategy.Starting();

            Log.Info("Running main processing loop");
            while (true)
            {
                // Update candles
                var addedCandles = new List<AddedCandleTimeframe>();
                foreach (var m in strategy.Markets)
                {
                    foreach (var t in strategy.Timeframes)
                    {
                        var candles = _candlesService.GetCandles(broker, m, t, true, forceUpdate: true)
                            .Where(c => c.IsComplete == 1)
                            .ToList();

                        if (candlesLookup[m][t].Count < candles.Count)
                        {
                            for (var i = candlesLookup[m][t].Count; i < candles.Count; i++)
                            {
                                candlesLookup[m][t].Add(candles[i]);
                                addedCandles.Add(new AddedCandleTimeframe(m, t, candles[i]));
                            }
                        }
                    }
                }

                if (addedCandles.Any()) // TODO reduce times this is called and include save
                {
                    // Update broker account
                    Log.Debug("Updating and saving broker account");

                    // Update strategy
                    strategy.UpdateIndicators(addedCandles);
                    strategy.UpdateBalances();
                    strategy.ProcessCandles(addedCandles);
                }

                Thread.Sleep(10000);
            }
        }

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

        private Type CompileStrategyAndGetStrategyMarkets(string selectedStrategyFilename)
        {
            // Compile strategy
            var code = File.ReadAllText(Path.Combine(_strategiesDirectory, $"{selectedStrategyFilename}.cs"));
            var strategyType = StrategyHelper.CompileStrategy(code, selectedStrategyFilename);

            if (strategyType == null)
            {
                Log.Error("Failed to compile strategy");
                return strategyType;
            }

            return strategyType;
        }

        /*
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
        }*/
    }
}