using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using Abt.Controls.SciChart.Model.DataSeries;
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

        public StrategyRunResultsChartViewModel()
        {
            DependencyContainer.ComposeParts(this);

            _testResultsStartedObserver = _results.TestRunStarted.Subscribe(UpdateChartData);
            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(UpdateChartData);

            Series = new XyDataSeries<DateTime, double>();
        }

        private void UpdateChartData(List<Trade> trades)
        {
            Series.Clear();
            var xvalues = new List<DateTime>();
            var yvalues = new List<double>();

            var startedTrades = trades.Where(t => t.EntryDateTime != null).ToList();

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
                            var candle = _brokersCandlesService.GetLastClosedCandle(t.Market, broker, Timeframe.D1, date, false);
                            var price = (decimal)(t.TradeDirection == TradeDirection.Long
                                    ? candle.Value.CloseBid
                                    : candle.Value.CloseAsk);
                            var stopDist = t.InitialStop.Value - t.EntryPrice;
                            var profit = (((decimal)price - t.EntryPrice.Value) / stopDist) * risk;
                            balance += (decimal)profit;
                        }
                    }

                    xvalues.Add(date);
                    yvalues.Add((double)balance);
                }
            }

            ((XyDataSeries<DateTime, double>)Series).Append(xvalues, yvalues);
        }

        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
            "Series", typeof(IDataSeries), typeof(StrategyRunResultsChartViewModel), new PropertyMetadata(default(IDataSeries)));

        public IDataSeries Series
        {
            get { return (IDataSeries)GetValue(SeriesProperty); }
            set { SetValue(SeriesProperty, value); }
        }
    }
}