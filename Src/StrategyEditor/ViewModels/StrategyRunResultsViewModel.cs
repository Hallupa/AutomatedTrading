using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using StrategyEditor.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Core.UI;
using TraderTools.Core.UI.ViewModels;
using TraderTools.Core.UI.Views;
using TraderTools.Indicators;

namespace StrategyEditor.ViewModels
{
    public class StrategyRunResultsViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private StrategyRunnerResultsService _results;
        [Import] public IBrokersService _brokersService;
        [Import] public UIService _uiService;
        private Dispatcher _dispatcher;
        private IDisposable _testResultsUpdatedObserver;
        private IDisposable _testResultsStartedObserver;

        public StrategyRunResultsViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            ChartViewModel = new ChartViewModel();
            ((INotifyPropertyChanged)ChartViewModel).PropertyChanged += ChartViewModelPropertyChanged;
            TradesViewModel = new TradeListViewModel();
            ChartViewModel.ChartTimeframe = Timeframe.H4;

            DependencyContainer.ComposeParts(this);

            _uiService.ViewTradeObservable.Subscribe(o =>
            {
                if (TradesViewModel.SelectedTrade == null) return;

                if (TradesViewModel.SelectedTrade.Timeframe != null)
                {
                    ChartViewModel.ChartTimeframe = TradesViewModel.SelectedTrade.Timeframe.Value;
                }

                ChartViewModel.ShowTrade(TradesViewModel.SelectedTrade,
                    ChartViewModel.ChartTimeframeOptions[ChartViewModel.SelectedChartTimeframeIndex], false,
                    s => { },
                    new List<(IIndicator Indicator, Color Color, bool ShowInLegend)>()
                    {
                        (new ExponentialMovingAverage(8), Colors.DarkBlue, true),
                        (new ExponentialMovingAverage(25), Colors.Blue, true),
                        (new ExponentialMovingAverage(50), Colors.Blue, true),
                        (new BollingerBand(1.5F, 20), Colors.Green, true),
                        (new BollingerBand(-1.5F, 20), Colors.Green, false)
                    },
                    _uiService.UseHeikenAshi);
            });

            ResultsViewModel = new TradesResultsViewModel(() =>
                {
                    lock (_results.Results)
                    {
                        return _results.Results.ToList();
                    }
                });

            _testResultsStartedObserver = _results.TestRunStarted.Subscribe(newResults =>
            {
                UpdateTrades();
                ResultsViewModel.UpdateResults();
                UpdateStatusColumn(newResults.Strategy?.BrokerKind);
            });

            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(newResults =>
            {
                UpdateTrades();
                ResultsViewModel.UpdateResults();
                UpdateStatusColumn(newResults.Strategy?.BrokerKind);
            });

            Task.Run(() =>
            {
                Log.Info("Updating strategy run results");
                UpdateTrades();
                ResultsViewModel.UpdateResults();
                Log.Info("Updated strategy run results");
            });

            TradesViewModel.ShowClosedTrades = true;
            TradesViewModel.ShowOpenTrades = true;
            TradesViewModel.ShowOrders = true;
            TradesViewModel.TradeListDisplayOptions &= ~TradeListDisplayOptionsFlag.Comments
                                                       & ~TradeListDisplayOptionsFlag.Strategies;
        }

        private void UpdateStatusColumn(BrokerKind? brokerKind)
        {
            if (brokerKind is BrokerKind.Trade)
            {
                TradesViewModel.TradeListDisplayOptions &= ~TradeListDisplayOptionsFlag.Status
                                                           & ~TradeListDisplayOptionsFlag.PoundsPerPip
                                                           & ~TradeListDisplayOptionsFlag.ResultR;
            }
            else
            {
                TradesViewModel.TradeListDisplayOptions |= TradeListDisplayOptionsFlag.Status
                                                           | TradeListDisplayOptionsFlag.PoundsPerPip
                                                           | TradeListDisplayOptionsFlag.ResultR;
            }
        }

        private void ChartViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedChartTimeframeIndex")
            {
                _uiService?.ViewTradeCommand.Execute(null);
            }
        }

        public TradeListViewModel TradesViewModel { get; set; }

        public ChartViewModel ChartViewModel { get; set; }

        public TradesResultsViewModel ResultsViewModel { get; }
        
        private void UpdateTrades()
        {
            var allTrades = _results.Results.OrderByDescending(x => x.OrderDateTime ?? x.EntryDateTime).ToList();

            _dispatcher.Invoke(() =>
            {
                // Clear trades in a single operation to speed it up
                if (allTrades.Count == 0)
                {
                    TradesViewModel.Trades.Clear();
                }

                TradesViewModel.Trades.Clear();

                // Add new trades
                if (TradesViewModel.Trades.Count == 0)
                {
                    TradesViewModel.Trades.AddRange(allTrades);
                }
                else
                {
                    for (var i = 0; i < allTrades.Count; i++)
                    {
                        var trade = allTrades[i];
                        if (i >= TradesViewModel.Trades.Count)
                        {
                            TradesViewModel.Trades.Add(trade);
                        }
                        else if (trade != TradesViewModel.Trades[i])
                        {
                            var existingIndex = TradesViewModel.Trades.IndexOf(trade);
                            if (existingIndex != -1)
                            {
                                TradesViewModel.Trades.Move(existingIndex, i);
                            }
                            else
                            {
                                TradesViewModel.Trades.Insert(i, trade);
                            }
                        }
                    }
                }
            });
        }
    }
}