using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Core.UI.ViewModels;

namespace AutomatedTraderDesigner.ViewModels
{
    public class StrategyRunResultsViewModel : TradeViewModelBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private StrategyRunnerResultsService _results;
        [Import] public IBrokersService _brokersService;
        [Import] public UIService _uiService;
        private Dispatcher _dispatcher;
        private IDisposable _testResultsUpdatedObserver;

        public StrategyRunResultsViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            LargeChartTimeframe = Timeframe.H4;

            DependencyContainer.ComposeParts(this);
            Broker = _brokersService.Brokers.First(x => x.Name == "FXCM");

            _uiService.ViewTradeObservable.Subscribe(o =>
            {
                if (SelectedTrade.Timeframe != null)
                {
                    LargeChartTimeframe = SelectedTrade.Timeframe.Value;
                }

                ViewTrade(SelectedTrade, false);
            });

            _uiService.ViewTradeSetupObservable.Subscribe(o =>
            {
                ViewTradeSetup(SelectedTrade);
            });

            ResultsViewModel = new TradesResultsViewModel(() =>
                {
                    lock (_results.Results)
                    {
                        return _results.Results.ToList();
                    }
                });

            _testResultsUpdatedObserver = _results.TestRunStarted.Subscribe(newResults =>
            {
                UpdateTrades();
                ResultsViewModel.UpdateResults();
            });

            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(newResults =>
            {
                UpdateTrades();
                ResultsViewModel.UpdateResults();
            });

            Task.Run(() =>
            {
                Log.Info("Updating strategy run results");
                UpdateTrades();
                ResultsViewModel.UpdateResults();
                Log.Info("Updated strategy run results");
            });

            ShowClosedTrades = true;
            ShowOpenTrades = true;
            ShowOrders = true;
        }

        public TradesResultsViewModel ResultsViewModel { get; }

        private void UpdateTrades()
        {
            var allTrades = _results.Results.OrderByDescending(x => x.OrderDateTime ?? x.EntryDateTime).ToList();

            _dispatcher.Invoke(() =>
            {
                // Clear trades in a single operation to speed it up
                if (allTrades.Count == 0)
                {
                    Trades.Clear();
                }

                Trades.Clear();

                // Add new trades
                if (Trades.Count == 0)
                {
                    Trades.AddRange(allTrades);
                }
                else
                {
                    for (var i = 0; i < allTrades.Count; i++)
                    {
                        var trade = allTrades[i];
                        if (i >= Trades.Count)
                        {
                            Trades.Add(trade);
                        }
                        else if (trade != Trades[i])
                        {
                            var existingIndex = Trades.IndexOf(trade);
                            if (existingIndex != -1)
                            {
                                Trades.Move(existingIndex, i);
                            }
                            else
                            {
                                Trades.Insert(i, trade);
                            }
                        }
                    }
                }
            });
        }
    }
}