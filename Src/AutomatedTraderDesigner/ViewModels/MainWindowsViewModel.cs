using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;

namespace AutomatedTraderDesigner.ViewModels
{
    public enum DisplayPages
    {
        Markets,
        CryptoTradeLog,
        RunStrategy,
        RunStrategyResults,
        RunCustomStrategy,
        StrategyAlerts,
        CryptoSummary,
        CryptoBalances,
        PatternSetups
    }

    public class MainWindowsViewModel : INotifyPropertyChanged
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Import] private BrokersService _brokersService;
        [Import] private TickDataService _tickDataService;
        [Import] private StrategyService _strategyService;
        [Import] private IBrokersCandlesService _candleService;
        [Import] private MarketsService _marketsService;
        [Import] private UIService _uiService;
        private bool _updatingCandles;

        [Import] public UIService UIService { get; private set; }

        #endregion

        #region Constructors
        public MainWindowsViewModel()
        {
            DependencyContainer.ComposeParts(this);

            CheckFXCandlesCommand = new DelegateCommand(CheckFXCandles);
            UpdateFXCandlesCommand = new DelegateCommand(UpdateFXCandles);
            UpdateTickDataCommand = new DelegateCommand(o => UpdateTickData());


            var fxcm = new FxcmBroker();
            var brokers = new IBroker[]
            {
                fxcm,
            };

            // Setup brokers and load accounts
            _brokersService.AddBrokers(brokers);

            Task.Run(Start); // If DLL binding errors, fix is to build in 64 bit

            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyDownEvent, new KeyEventHandler(UIElement_OnPreviewKeyDown), true);
        }

        private void UIElement_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _uiService.RaiseF5Pressed();
            }
        }

        private void UpdateTickData()
        {
            Task.Run(() =>
            {
                var broker = _brokersService.Brokers.First(x => x.Name == "FXCM");
                foreach (var market in _marketsService.GetMarkets())
                {
                    _tickDataService.GetTickData(market.Name, broker, true);

                    break;
                }
            });
        }

        private void CheckFXCandles(object obj)
        {
            Task.Run(() =>
            {
                var fxcm = _brokersService.Brokers.First(x => x.Name == "FXCM");

                var timeframes = new[] { Timeframe.M1, Timeframe.H1, Timeframe.H2, Timeframe.H4, Timeframe.H8, Timeframe.D1 };
                var timeframesExcludingM1 = new[] { Timeframe.H1, Timeframe.H2, Timeframe.H4, Timeframe.H8, Timeframe.D1 };

                foreach (var market in _marketsService.GetMarkets())
                {
                    Log.Info($"Checking {market}");
                    var timeframeAllCandles = new TimeframeLookup<IList<Candle>>();
                    var timeframeCandleIndexes = new TimeframeLookup<int>();

                    foreach (var timeframe in timeframes)
                    {
                        timeframeAllCandles[timeframe] = _candleService.GetCandles(fxcm, market.Name, timeframe, false);
                        timeframeCandleIndexes[timeframe] = -1;
                    }


                    foreach (var timeframe in timeframes)
                    {
                        var candles = timeframeAllCandles[timeframe];

                        if (candles.Count == 0) continue;

                        for (var i = 1; i < candles.Count; i++)
                        {
                            if ((new DateTime(candles[i].CloseTimeTicks) - new DateTime(candles[i - 1].CloseTimeTicks)).TotalDays > 5)
                            {
                                Log.Error($"{market.Name} {timeframe} has gap in candles of more than 3 days");
                            }
                        }
                    }

                    for (var i = 0; i < timeframeAllCandles[Timeframe.M1].Count; i++)
                    {
                        var m1Candle = timeframeAllCandles[Timeframe.M1][i];

                        foreach (var timeframe in timeframesExcludingM1)
                        {
                            var changes = 0;
                            for (var ii = timeframeCandleIndexes[timeframe] + 1; ii < timeframeAllCandles[timeframe].Count; ii++)
                            {
                                var timeframeCandle = timeframeAllCandles[timeframe][ii];

                                // Look for completed candle
                                if (timeframeCandle.CloseTimeTicks <= m1Candle.CloseTimeTicks)
                                {
                                    // Add candle
                                    timeframeCandleIndexes[timeframe] = ii;
                                    changes++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (changes > 1)
                            {
                                Log.Error($"{market.Name} {timeframe} doesn't have corresponding M1 candles");
                            }
                        }
                    }

                    foreach (var timeframe in timeframes)
                    {
                        _candleService.UnloadCandles(market.Name, timeframe, fxcm);
                    }

                    GC.Collect();

                    Log.Info($"Checked {market.Name}");
                }
            });
        }

        private void UpdateFXCandles(object obj)
        {
            if (_updatingCandles) return;
            _updatingCandles = true;
            var dispatcher = Dispatcher.CurrentDispatcher;
            var fxcm = _brokersService.Brokers.First(x => x.Name == "FXCM");

            Task.Run(() =>
            {
                var producerConsumer =
                    new ProducerConsumer<(string Market, Timeframe Timeframe)>(3,
                        data =>
                        {
                            Log.Info($"Updating {data.Timeframe} candles for {data.Market}");
                            _candleService.UpdateCandles(fxcm, data.Market, data.Timeframe);
                            _candleService.UnloadCandles(data.Market, data.Timeframe, fxcm);
                            Log.Info($"Updated {data.Timeframe} candles for {data.Market}");
                            return ProducerConsumerActionResult.Success;
                        });


                foreach (var market in _marketsService.GetMarkets())
                {
                    foreach (var timeframe in new[]
                        {Timeframe.D1, Timeframe.H8, Timeframe.H4, Timeframe.H2, Timeframe.H1, Timeframe.M1, Timeframe.M15})
                    {
                        producerConsumer.Add((market.Name, timeframe));
                    }
                }

                producerConsumer.SetProducerCompleted();
                producerConsumer.Start();
                producerConsumer.WaitUntilConsumersFinished();

                dispatcher.Invoke(() => { _updatingCandles = false; });
                Log.Info("Updated FX candles");
            });
        }

        #endregion

        #region Properties
        [Import]
        public ChartingService ChartingService { get; private set; }

        public DelegateCommand UpdateFXCandlesCommand { get; private set; }

        public DelegateCommand CheckFXCandlesCommand { get; private set; }

        public DelegateCommand UpdateTickDataCommand { get; private set; }

        #endregion

        #region Methods

        private void Start()
        {
            // Connect to brokers
            _brokersService.Connect();
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}