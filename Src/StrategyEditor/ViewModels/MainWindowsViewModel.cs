using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using StrategyEditor.Services;
using Hallupa.Library;
using log4net;
using StrategyEditor.Views;
using TraderTools.Basics;
using TraderTools.Basics.Helpers;
using TraderTools.Brokers.Binance;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;
using TraderTools.Simulation;
using Dispatcher = System.Windows.Threading.Dispatcher;

namespace StrategyEditor.ViewModels
{
    public enum DisplayPages
    {
        RunCustomStrategy,
        RunStrategyResults,
        RunStrategyResultsChart,
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
                new BinanceBroker("TODO", "TODO"), 
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
            if (string.IsNullOrEmpty(_uiService.SelectedStrategyFilename))
            {
                MessageBox.Show("Select strategy to update candles");
                return;
            }

            var strategyType = StrategyHelper.CompileStrategy(_uiService.SelectedCodeText);
            if (strategyType == null)
            {
                MessageBox.Show("Unable to compile strategy");
                return;
            }

            var strategy = (StrategyBase)Activator.CreateInstance(strategyType);

            _updatingCandles = true;
            var dispatcher = Dispatcher.CurrentDispatcher;
            var fxcm = _brokersService.Brokers.First(x => x.Name == "FXCM");
            var timeframes = strategy.Timeframes.Union(new[] { Timeframe.M1 }).ToArray();
            var markets = strategy.Markets;

            if (markets == null)
            {
                markets = StrategyBase.GetDefaultMarkets();
            }

            var view = new ProgressView { Owner = Application.Current.MainWindow };
            view.TextToShow.Text = "Updating candles";

            Task.Run(() =>
            {
                try
                {
                    CandlesHelper.UpdateCandles(
                        fxcm,
                        _candleService,
                        markets,
                        timeframes,
                        updateProgressAction: s =>
                        {
                            _dispatcher.BeginInvoke((Action)(() =>
                           {
                               view.TextToShow.Text = s;
                           }));
                        });
                }
                catch (Exception ex)
                {
                    Log.Error("Unable to update candles", ex);
                }

                dispatcher.Invoke(() =>
                {
                    view.Close();
                    MessageBox.Show("Updating candles complete");
                    _updatingCandles = false;
                });
            });

            view.ShowDialog();
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