using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Core.Broker;
using TraderTools.Core.Services;
using TraderTools.Core.UI.ViewModels;

namespace AutomatedTrader.ViewModels
{
    public class StrategyRunLiveResultsViewModel : TradeViewModelBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] public BrokersService _brokersService;
        [Import] public UIService _uiService;
        private Dispatcher _dispatcher;
        private IDisposable _testResultsUpdatedObserver;
        private BrokerAccount _account;
        private IDisposable _accountUpdatedObserver;

        public StrategyRunLiveResultsViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            LargeChartTimeframe = Timeframe.M1;
            DependencyContainer.ComposeParts(this);
            Broker = _brokersService.Brokers.First(x => x.Name == "FXCM");

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
                lock (Trades)
                {
                    return Trades.ToList();
                }
            })
            {
                ShowProfit = true,
                AdvStrategyNaming = true,
                ShowSubOptions = false,
                SubItemsIndex = 1
            };

            _account = _brokersService.AccountsLookup[Broker];
            _accountUpdatedObserver = _account.AccountUpdatedObservable.Subscribe(d =>
            {
                _dispatcher.Invoke(RefreshUI);
            });

            RefreshUI();
        }

        public TradesResultsViewModel ResultsViewModel { get; }

        private void RefreshUI()
        {
            Trades.Clear();

            foreach (var trade in _account.Trades
                .Where(x => x.OrderDateTime != null || x.EntryDateTime != null)
                .OrderByDescending(x => int.Parse(x.Id)))
            {
                Trades.Add(trade);
            }

            ResultsViewModel.UpdateResults();
            //SummaryViewModel.Update(Trades.ToList());
        }
    }
}