using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Hallupa.Library;
using Hallupa.Library.Extensions;
using Hallupa.TraderTools.Basics;
using Hallupa.TraderTools.Simulation;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.RenderableSeries;
using StrategyEditor.Services;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace StrategyEditor.ViewModels
{
    public class StrategyRunResultsChartViewModel : DependencyObject
    {
        [Import] private StrategyRunnerResultsService _results;
        [Import] private IBrokersCandlesService _brokersCandlesService;
        [Import] private IBrokersService _brokerService;
        private IDisposable _testResultsStartedObserver;
        private IDisposable _testResultsUpdatedObserver;
        private Dispatcher _dispatcher;

        public StrategyRunResultsChartViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            DependencyContainer.ComposeParts(this);

            SeriesList = new ObservableCollection<IRenderableSeries>();
            _testResultsStartedObserver = _results.TestRunStarted.Subscribe(UpdateChartData);
            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(UpdateChartData);
            ClearResultsChartCommand = new DelegateCommand(o => ClearResultsChart());
        }

        public static readonly DependencyProperty SeriesListProperty = DependencyProperty.Register(
            "SeriesList", typeof(ObservableCollection<IRenderableSeries>), typeof(StrategyRunResultsChartViewModel), new PropertyMetadata(default(ObservableCollection<IRenderableSeries>)));

        public ObservableCollection<IRenderableSeries> SeriesList
        {
            get { return (ObservableCollection<IRenderableSeries>)GetValue(SeriesListProperty); }
            set { SetValue(SeriesListProperty, value); }
        }

        public DelegateCommand ClearResultsChartCommand { get; private set; }

        private void ClearResultsChart()
        {
            SeriesList.Clear();
        }

        private void UpdateChartData((List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances) d)
        {
            var series = new XyDataSeries<DateTime, double>();

            Task.Run(() =>
            {
                var xvalues = new List<DateTime>();
                var yvalues = new List<double>();

                var startedTrades = d.Trades.Where(t => t.EntryDateTime != null && !t.Ignore).ToList();

                if (d.Strategy.BrokerKind == BrokerKind.SpreadBet)
                {
                    var earliest = startedTrades.OrderBy(t => t.EntryDateTime).First().EntryDateTime.Value.Date;
                    var latest = DateTime.UtcNow;
                    var broker = _brokerService.GetBroker("FXCM");

                    for (var date = earliest; date <= latest; date = date.AddDays(1))
                    {
                        var balance = 10000M;
                        var currentTrades = d.Trades.Where(t => t.EntryDateTime <= date).ToList();
                        foreach (var t in currentTrades)

                        {
                            if (date >= t.CloseDateTime)
                            {
                                balance += (decimal)t.Profit.Value;
                            }
                            else
                            {
                                var risk = t.RiskAmount.Value;
                                var candle =
                                    _brokersCandlesService.GetLastClosedCandle(t.Market,
                                        _brokerService.GetBroker(t.Broker), Timeframe.D1, date,
                                        false);
                                var price = (decimal)(t.TradeDirection == TradeDirection.Long
                                    ? candle.Value.CloseBid
                                    : candle.Value.CloseAsk);


                                /*var stopDist = t.InitialStop.Value - t.EntryPrice;
                                var profit = (((decimal)price - t.EntryPrice.Value) / stopDist) * risk;*/
                                var profit = price * t.EntryQuantity.Value -
                                             t.EntryPrice.Value * t.EntryQuantity.Value; //TODO Add commission
                                balance += (decimal)profit;
                            }

                            xvalues.Add(date);
                            yvalues.Add((double)balance);
                        }
                    }
                }
                else
                {


                    if (startedTrades.Count > 0)
                    {
                        var candlesLookup = new Dictionary<(string Market, IBroker Broker), List<Candle>>();
                        var broker = _brokerService.GetBroker(d.Strategy.Broker);
                        var earliest = startedTrades.OrderBy(t => t.EntryDateTime).First().EntryDateTime.Value.Date;
                        var latest = DateTime.UtcNow;

                        var assetBalances = d.InitialAssetBalances.ToDictionary(x => x.Key,
                            x => new AssetBalance(x.Key, x.Value.Balance));
                        var orderedTrades = d.Trades.Where(t => t.EntryDateTime <= latest).OrderBy(x => x.EntryDateTime)
                            .ToList();
                        var tradeIndex = 0;

                        for (var date = earliest; date <= latest; date = date.AddDays(1))
                        {
                            while (tradeIndex < orderedTrades.Count && orderedTrades[tradeIndex].EntryDateTime <= date)
                            {
                                var t = orderedTrades[tradeIndex];
                                tradeIndex++;

                                assetBalances.UpdateAssetBalance(t);
                            }

                            var valueUsd = GetAssetBalancesUsdtValue(date, candlesLookup, assetBalances, broker);
                            xvalues.Add(date);
                            yvalues.Add((double)valueUsd);

                        }



                        series.Append(xvalues, yvalues);
                    }
                }

                _dispatcher.Invoke(() =>
                {
                    var renderableSeries = new FastLineRenderableSeries
                    {
                        DataSeries = series,
                        StrokeThickness = 2
                    };

                    SeriesList.Add(renderableSeries);
                });
            });
        }

        private decimal GetAssetBalancesUsdtValue(
            DateTime currentDateTimeUtc, Dictionary<(string Market, IBroker Broker), List<Candle>> candlesLookup,
            Dictionary<string, AssetBalance> assetBalances, IBroker broker)
        {
            var totalUsdtValue = 0M;
            foreach (var assetBalance in assetBalances)
            {
                if (assetBalance.Value.Asset == "USDT")
                {
                    totalUsdtValue += assetBalance.Value.Balance;
                    continue;
                }

                var usdtMarket = $"{assetBalance.Key}USDT";
                if (!candlesLookup.ContainsKey((usdtMarket, broker)))
                {
                    candlesLookup[(usdtMarket, broker)] = _brokersCandlesService.GetCandles(broker, usdtMarket, Timeframe.D1, false);
                }

                var candles = candlesLookup[(usdtMarket, broker)];
                var lastClosedCandleIndex = candles.BinarySearchGetItem(
                    i => candles[i].CloseTimeTicks, 0, currentDateTimeUtc.Ticks,
                    BinarySearchMethod.PrevLowerValueOrValue);

                var candle = candles[lastClosedCandleIndex];
                var assetUsdtValue = assetBalance.Value.Balance * (decimal)candle.CloseBid;
                totalUsdtValue += assetUsdtValue;
            }

            return totalUsdtValue;
        }
    }
}