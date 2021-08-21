using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Threading;
using Abt.Controls.SciChart.Model.DataSeries;
using Hallupa.Library;
using Hallupa.TraderTools.Brokers.Binance;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.UI;

namespace StrategyEditor.ViewModels
{
    public class MachineLearningChartViewModel
    {
        [Import] private IBrokersService _brokersService;
        [Import] private IBrokersCandlesService _candlesService;
        private Timeframe _selectedTimeframe;
        private bool _useHeikenAshi;
        private BinanceBroker _broker;

        public MachineLearningChartViewModel()
        {
            DependencyContainer.ComposeParts(this);

            _broker = (BinanceBroker) _brokersService.Brokers.First(b => b.Name == "Binance");
            
            ChartViewModel = new ChartViewModel();
            ViewMarketCommand = new DelegateCommand(o => ViewMarket());
            TimeframeOptions = new List<Timeframe>();
            TimeframeOptions.AddRange(new[] {Timeframe.M15, Timeframe.H1, Timeframe.H2, Timeframe.H4, Timeframe.D1});
            SelectedTimeframe = Timeframe.H1;
            Market = "BTCUSDT";
        }

        public ChartViewModel ChartViewModel { get; private set; }
        public DelegateCommand ViewMarketCommand { get; }
        public string Market { get; set; }
        public List<Timeframe> TimeframeOptions { get; }

        public bool UseHeikenAshi
        {
            get => _useHeikenAshi;
            set
            {
                _useHeikenAshi = value;
                ViewMarket();
            }
        }

        public Timeframe SelectedTimeframe
        {
            get => _selectedTimeframe;
            set
            {
                _selectedTimeframe = value;
                ViewMarket();
            }
        }

        private void ViewMarket()
        {
            if (string.IsNullOrEmpty(Market)) return;

            var candles = _candlesService.GetDerivedCandles(_broker, Market, SelectedTimeframe);

            if (UseHeikenAshi) candles = candles.CreateHeikinAshiCandles();

            var priceDataSeries = new OhlcDataSeries<DateTime, double>();
            var xvalues = new List<DateTime>();
            var openValues = new List<double>();
            var highValues = new List<double>();
            var lowValues = new List<double>();
            var closeValues = new List<double>();

            for (var i = 0; i < candles.Count; i++)
            {
                var time = new DateTime(candles[i].CloseTimeTicks);

                xvalues.Add(time);
                openValues.Add((double) candles[i].OpenBid);
                highValues.Add((double) candles[i].HighBid);
                lowValues.Add((double) candles[i].LowBid);
                closeValues.Add((double) candles[i].CloseBid);
            }

            priceDataSeries.Append(xvalues, openValues, highValues, lowValues, closeValues);
            priceDataSeries.SeriesName = "Price";

            //ChartViewModel.ChartPaneViewModels.Clear();
            ChartHelper.SetChartViewModelPriceData(candles, ChartViewModel);

            /*var pricePaneVm = new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
            {
                IsFirstChartPane = false,
                IsLastChartPane = false,
                Height = 400
            };
            ChartViewModel.ChartPaneViewModels.Add(pricePaneVm);*/

           // ChartHelper.SetChartViewModelPriceData(candles, ChartViewModel);

            /*var indicatorChartPaneViewModel =
                new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
                {
                    IsFirstChartPane = false,
                    IsLastChartPane = true,
                    Height = 200
                };

            ChartHelper.AddIndicator(
                indicatorChartPaneViewModel,
                ViewPairText,
                new StochasticRelativeStrengthIndex(),
                Colors.Blue,
                timeframe,
                candles);
            ChartViewModel.ChartPaneViewModels.Add(indicatorChartPaneViewModel);*/
        }
    }
}