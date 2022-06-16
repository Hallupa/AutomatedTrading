using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CryptoTrader;
using StrategyEditor.Services;
using Hallupa.Library;
using Hallupa.TraderTools.Brokers.Binance;
using log4net;
using StrategyEditor.Views;
using TraderTools.Basics;
using TraderTools.Basics.Helpers;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;
using TraderTools.Simulation;
using Dispatcher = System.Windows.Threading.Dispatcher;

namespace StrategyEditor.ViewModels
{
    public enum DisplayPages
    {
        RunStrategy,
        StrategyViewTrade,
        StrategyEquity,
        StrategyViewAllTrades,
        MachineLearning
    }

    public class MainWindowsViewModel : INotifyPropertyChanged
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Import] private IBrokersService _brokersService;
        [Import] private IBrokersCandlesService _candleService;
        [Import] private MarketsService _marketsService;
        [Import] private IMarketDetailsService _marketDetailsService;
        [Import] private UIService _uiService;
        [Import] public UIService UIService { get; private set; }
        private bool _updatingCandles;
        private Dispatcher _dispatcher;
        private bool _includeCrypto = true;

        #endregion

        #region Constructors
        public MainWindowsViewModel()
        {
            DependencyContainer.ComposeParts(this);

            _dispatcher = Dispatcher.CurrentDispatcher;

            UpdateFXCandlesCommand = new DelegateCommand(UpdateFXCandles);

            var brokers = new IBroker[]
            {
                new FxcmBroker(),
                new BinanceBroker("", ""),
            };

            // Setup brokers and load accounts
            _brokersService.AddBrokers(brokers);

            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyDownEvent, new KeyEventHandler(UIElement_OnPreviewKeyDown), true);
            LoginOutViewModel = new LoginOutViewModel();
        }

        public LoginOutViewModel LoginOutViewModel { get; private set; }

        private void UIElement_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _uiService.RaiseF5Pressed();
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _uiService.RaiseControlSPressed();
            }
        }

        private void UpdateFXCandles(object obj)
        {
            if (_updatingCandles) return;
            var dispatcher = Dispatcher.CurrentDispatcher;
            _updatingCandles = true;

            if (_includeCrypto)
            {
                var candleUpdater = new BinanceCandlesUpdater(
                    (BinanceBroker)_brokersService.Brokers.First(b => b.Name == "Binance"),
                    _candleService,
                    _marketDetailsService);

                candleUpdater
                    .RunAsync()
                    .ContinueWith(
                        t => Task.Run(() => { dispatcher.Invoke(() => { _updatingCandles = false; }); }));
            }
            else
            {

            }
        }

        #endregion

        #region Properties
        [Import]
        public ChartingService ChartingService { get; private set; }

        public DelegateCommand UpdateFXCandlesCommand { get; private set; }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}