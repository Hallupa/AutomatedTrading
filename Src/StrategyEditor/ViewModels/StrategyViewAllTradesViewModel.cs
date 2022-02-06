using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using Abt.Controls.SciChart.Model.DataSeries;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using Hallupa.TraderTools.Brokers.Binance;
using StrategyEditor.Services;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.UI;
using TraderTools.Core.UI.Services;
using TraderTools.Indicators;

namespace StrategyEditor.ViewModels
{
    public class StrategyViewAllTradesViewModel : INotifyPropertyChanged
    {
        [Import] private IBrokersService _brokersService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        [Import] private IMarketDetailsService _marketDetailsService;
        [Import] public ChartingService ChartingService { get; private set; }
        [Import] private StrategyRunnerResultsService _results;
        private bool _viewPairEnabled = true;
        private Dispatcher _dispatcher;
        private BinanceBroker _broker;
        private IDisposable _testResultsStartedObserver;
        private IDisposable _testResultsUpdatedObserver;
        private List<Trade> _trades;
        private List<Candle> _currentCandles;
        private string _currentPair;
        private bool _isHeikinAshi;
        private string _viewPairText;

        public StrategyViewAllTradesViewModel()
        {
            DependencyContainer.ComposeParts(this);

            _broker = (BinanceBroker)_brokersService.Brokers.First(b => b.Name == "Binance");
            _dispatcher = Dispatcher.CurrentDispatcher;
            ViewPairCommand = new DelegateCommand(o => ViewPair());
            ChartViewModel.ChartTimeframeChangedAction += ChartTimeframeChangedAction;

            _testResultsStartedObserver = _results.TestRunStarted.Subscribe(newResults =>
            {
                _trades = null;
                if (ChartViewModel.ChartPaneViewModels != null && ChartViewModel.ChartPaneViewModels.Count > 0)
                    ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Clear();
            });

            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(newResults =>
            {
                _trades = newResults.Trades;
                if (_trades.Count > 0)
                {
                    ViewPairText = _trades[0].Market;
                }

                ViewPair();
            });
        }

        public DelegateCommand ViewPairCommand { get; }

        public bool IsHeikinAshi
        {
            get => _isHeikinAshi;
            set
            {
                _isHeikinAshi = value;
                ViewPair(false);
            }
        }

        public string ViewPairText
        {
            get => _viewPairText;
            set
            {
                _viewPairText = value;
                OnPropertyChanged();
            }
        }


        public ChartViewModel ChartViewModel { get; } = new ChartViewModel();

        public bool ViewPairEnabled
        {
            get => _viewPairEnabled;
            set
            {
                _viewPairEnabled = value;
                OnPropertyChanged();
            }
        }

        public void SetCandlesOnChart(List<Candle> candles, string pair, string seriesName)
        {
            ChartViewModel.ChartPaneViewModels.Clear();
            ViewPairText = pair;
            ChartHelper.SetChartViewModelPriceData(candles, ChartViewModel, seriesName);
        }

        private void ChartTimeframeChangedAction()
        {
            ViewPair(false);
        }

        private void ViewPair(bool refreshTradeAnnotations = true)
        {
            if (string.IsNullOrEmpty(ViewPairText)) return;

            ViewPairEnabled = false;

            Task.Run(() =>
            {
                var candles = _candlesService.GetDerivedCandles(_broker, ViewPairText, ChartViewModel.ChartTimeframe);
                if (candles.Count == 0)
                {
                    _dispatcher.Invoke((Action)(() =>
                    {
                        ViewPairEnabled = true;
                        ChartViewModel.ChartPaneViewModels.Clear();
                    }));

                    return;
                }

                if (IsHeikinAshi) candles = candles.CreateHeikinAshiCandles();

                _currentCandles = candles;
                _currentPair = ViewPairText;

                var priceDataSeries = new OhlcDataSeries<DateTime, double>();
                var xvalues = new List<DateTime>();
                var openValues = new List<double>();
                var highValues = new List<double>();
                var lowValues = new List<double>();
                var closeValues = new List<double>();

                foreach (var c in candles)
                {
                    var time = new DateTime(c.CloseTimeTicks, DateTimeKind.Utc).ToUniversalTime();

                    xvalues.Add(time);
                    openValues.Add((double)c.OpenBid);
                    highValues.Add((double)c.HighBid);
                    lowValues.Add((double)c.LowBid);
                    closeValues.Add((double)c.CloseBid);
                }

                priceDataSeries.Append(xvalues, openValues, highValues, lowValues, closeValues);
                priceDataSeries.SeriesName = "Price";

                _dispatcher.Invoke(() =>
                {
                    //ChartViewModel.ChartPaneViewModels.Clear();
                    ChartHelper.SetChartViewModelPriceData(candles, ChartViewModel);//, null);

                    if (refreshTradeAnnotations) AddTradeMarkers();

                    /*var pricePaneVm = new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
                    {
                        IsFirstChartPane = false,
                        IsLastChartPane = false,
                        Height = 400
                    };
                    ChartViewModel.ChartPaneViewModels.Add(pricePaneVm);

                    ChartHelper.SetChartViewModelPriceData(candles, ChartViewModel);//, pricePaneVm, false);*/

                    /*var indicatorChartPaneViewModel =
                        new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
                        {
                            IsFirstChartPane = false,
                            IsLastChartPane = true,
                            Height = 200
                        };
                    */
                    /*ChartHelper.AddIndicator(
                        indicatorChartPaneViewModel,
                        ViewPairText,
                        new StochasticRelativeStrengthIndex(),
                        Colors.Blue,
                        timeframe,
                        candles);
                    ChartViewModel.ChartPaneViewModels.Add(indicatorChartPaneViewModel);*/

                    //ChartHelper.add
                    /*ChartPaneViewModels[0].ChartSeriesViewModels.Clear();
                    ChartPaneViewModels[0].ChartSeriesViewModels.Add(new ChartSeriesViewModel(
                        priceDataSeries,
                        new FastCandlestickRenderableSeries
                        {
                            AntiAliasing = false
                        }));*/

                    /*var indicatorSeries = ChartHelper.CreateIndicatorSeries(
                        ViewPairText,
                        new T3CommodityChannelIndex(),
                        Colors.Blue,
                        timeframe,
                        candles);

                    var indicatorViewModel = new ChartSeriesViewModel(indicatorSeries, new FastLineRenderableSeries
                    {
                        AntiAliasing = false,
                        SeriesColor = Colors.Blue,
                        StrokeThickness = 2
                    });*/

                    /*var pvm = new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
                    {
                        IsFirstChartPane = false,
                        IsLastChartPane = true,
                        Height = 100
                    };*/

                    //ChartSeriesViewModels.Add(indicatorViewModel);

                    Task.Run(() =>
                    {
                        _dispatcher.Invoke((Action)(() =>
                        {
                            /*var min = priceDataSeries.Count - 220;
                            var max = priceDataSeries.Count + 5;

                            if (min < 0) min = 0;
                            if (min <= XVisibleRange.Max)
                            {
                                XVisibleRange.Min = min;
                                XVisibleRange.Max = max;
                            }
                            else
                            {
                                XVisibleRange.Max = max;
                                XVisibleRange.Min = min;
                            }*/

                            ViewPairEnabled = true;
                        }));
                    });
                });
            });
        }

        private void AddTradeMarkers()
        {
            if (ChartViewModel.ChartPaneViewModels != null && ChartViewModel.ChartPaneViewModels.Count > 0)
            {
                var annotations = new AnnotationCollection();

                var l = new List<AnnotationBase>();
                foreach (var t in _trades)
                {
                    if (t.Market != _currentPair || t.TradeDirection == null) continue;

                    if (t.EntryDateTime != null && t.EntryPrice != null)
                    {
                        l.Add(ChartHelper.CreateBuySellMarker(t.TradeDirection.Value, null, t.EntryDateTime.Value, t.EntryPrice.Value, true));
                    }

                    if (t.CloseDateTime != null && t.ClosePrice != null)
                    {
                        l.Add(ChartHelper.CreateBuySellMarker(t.TradeDirection.Value == TradeDirection.Long ? TradeDirection.Short : TradeDirection.Long,
                            null, t.CloseDateTime.Value, t.ClosePrice.Value, true));
                    }
                }

                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection(l);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}