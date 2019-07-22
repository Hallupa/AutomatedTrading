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
using TraderTools.Core.Services;
using TraderTools.Core.UI.ViewModels;

namespace AutomatedTraderDesigner.ViewModels
{
    public class StrategyRunResultsViewModel : TradeViewModelBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private StrategyRunnerResultsService _results;
        [Import] public BrokersService _brokersService;
        [Import] public UIService _uiService;
        private Dispatcher _dispatcher;
        private IDisposable _testResultsUpdatedObserver;

        public StrategyRunResultsViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            DependencyContainer.ComposeParts(this);
            Broker = _brokersService.Brokers.First(x => x.Name == "FXCM");

            LargeChartTimeframe = Timeframe.M15;

            _uiService.ViewTradeObservable.Subscribe(o =>
            {
                ViewTrade(SelectedTrade);
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

            _testResultsUpdatedObserver = _results.TestResultsUpdated.Subscribe(newResults =>
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
        }

        public TradesResultsViewModel ResultsViewModel { get; }

        private void UpdateTrades()
        {
            var allTrades = _results.Results.OrderByDescending(x => x.OrderDateTime.Value).ToList();

            _dispatcher.Invoke(() =>
            {
                // Remove obsolete trades
                for (var i = Trades.Count - 1; i >= 0; i--)
                {
                    if (!allTrades.Contains(Trades[i]))
                    {
                        Trades.RemoveAt(i);
                    }
                }

                // Add new trades
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
            });
        }
    }
}