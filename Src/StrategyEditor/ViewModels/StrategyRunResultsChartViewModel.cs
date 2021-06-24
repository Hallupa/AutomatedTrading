using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Abt.Controls.SciChart.Model.DataSeries;
using Abt.Controls.SciChart.Visuals.RenderableSeries;
using Hallupa.Library;
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
            get { return (ObservableCollection<IRenderableSeries>) GetValue(SeriesListProperty); }
            set { SetValue(SeriesListProperty, value); }
        }

        public DelegateCommand ClearResultsChartCommand { get; private set; }
        
        private void ClearResultsChart()
        {
            SeriesList.Clear();
        }

        private void UpdateChartData(List<Trade> trades)
        {
            var series = new XyDataSeries<DateTime, double>();

            Task.Run(() =>
            {
                var xvalues = new List<DateTime>();
                var yvalues = new List<double>();

                var startedTrades = trades.Where(t => t.EntryDateTime != null && !t.Ignore).ToList();

                if (startedTrades.Count > 0)
                {
                    var earliest = startedTrades.OrderBy(t => t.EntryDateTime).First().EntryDateTime.Value.Date;
                    var latest = DateTime.UtcNow;
                    var broker = _brokerService.GetBroker("FXCM");

                    for (var date = earliest; date <= latest; date = date.AddDays(1))
                    {
                        var balance = 10000M;
                        var currentTrades = trades.Where(t => t.EntryDateTime <= date).ToList();
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
                                    _brokersCandlesService.GetLastClosedCandle(t.Market, _brokerService.GetBroker(t.Broker), Timeframe.D1, date,
                                        false);
                                var price = (decimal)(t.TradeDirection == TradeDirection.Long
                                    ? candle.Value.CloseBid
                                    : candle.Value.CloseAsk);


                                /*var stopDist = t.InitialStop.Value - t.EntryPrice;
                                var profit = (((decimal)price - t.EntryPrice.Value) / stopDist) * risk;*/
                                var profit = price * t.EntryQuantity.Value - t.EntryPrice.Value * t.EntryQuantity.Value; //TODO Add commission
                                balance += (decimal)profit;
                            }
                        }

                        xvalues.Add(date);
                        yvalues.Add((double)balance);
                    }

                    series.Append(xvalues, yvalues);
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
    }
}