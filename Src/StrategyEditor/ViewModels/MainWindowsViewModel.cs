using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Basics.Helpers;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;
using Dispatcher = System.Windows.Threading.Dispatcher;

namespace AutomatedTraderDesigner.ViewModels
{
    public enum DisplayPages
    {
        RunCustomStrategy,
        RunStrategyResults
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
        private bool _updatingCandles;

        [Import] public UIService UIService { get; private set; }

        #endregion

        #region Constructors
        public MainWindowsViewModel()
        {
            DependencyContainer.ComposeParts(this);

            UpdateFXCandlesCommand = new DelegateCommand(UpdateFXCandles);

            var fxcm = new FxcmBroker();
            var brokers = new IBroker[]
            {
                fxcm,
            };

            // Setup brokers and load accounts
            _brokersService.AddBrokers(brokers);

            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyDownEvent, new KeyEventHandler(UIElement_OnPreviewKeyDown), true);
        }

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
            _updatingCandles = true;
            var dispatcher = Dispatcher.CurrentDispatcher;
            var fxcm = _brokersService.Brokers.First(x => x.Name == "FXCM");

            ((FxcmBroker)fxcm).SetUsernamePassword("", "", "GBREAL");
            fxcm.Connect();

            Task.Run(() =>
            {
                CandlesHelper.UpdateCandles(
                    fxcm, _candleService, _marketDetailsService.GetAllMarketDetails().Select(x => x.Name), 
                    new[] { Timeframe.D1, Timeframe.H8, Timeframe.H4, Timeframe.H2, Timeframe.H1, Timeframe.M1, Timeframe.M15 });

                dispatcher.Invoke(() => { _updatingCandles = false; });
            });
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