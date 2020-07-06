using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using Hallupa.Library.UI.Views;
using log4net;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Trading;
using TraderTools.Core.UI.ViewModels;
using TraderTools.Simulation;
using TraderTools.Strategy;

namespace AutomatedTraderDesigner.ViewModels
{
    public class StrategyRunViewModel : INotifyPropertyChanged
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string _strategiesDirectory;
        private string _codeText;
        [Import] private StrategyRunnerResultsService _results;
        [Import] private UIService _uiService;
        [Import] private IBrokersService _brokersService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private ITradeDetailsAutoCalculatorService _tradeCalculatorService;
        [Import] private IMarketDetailsService _marketDetailsService;
        private bool _runStrategyEnabled = true;
        private bool _stopRun = false;
        private bool _stopStrategyEnabled;
        private IDisposable _testResultsUpdatedObserver;
        private string _selectedStrategyFilename;
        private string _defaultStrategyText;
        private Dispatcher _dispatcher;
        private ProducerConsumer<(Type StrategyType, MarketDetails Market)> _producerConsumer;
        private IBroker _broker;
        private Type _strategyType;
        private byte[] _strategyHash;

        public StrategyRunViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            DependencyContainer.ComposeParts(this);
            SaveCommand = new DelegateCommand(o =>
            {
                Save();
            });

            CreateStrategyCommand = new DelegateCommand(o => CreateStrategy());
            DeleteStrategyCommand = new DelegateCommand(o => DeleteStrategy());
            RunStrategyCommand = new DelegateCommand(RunStrategyClicked);
            StopStrategyCommand = new DelegateCommand(StopStrategyClicked);
            _broker = _brokersService.GetBroker("FXCM");

            _strategiesDirectory = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName);

            if (!Directory.Exists(_strategiesDirectory))
            {
                Directory.CreateDirectory(_strategiesDirectory);
            }

            LoadDefaultStrategyText();
            RefreshStrategyFilenames();

            ResultsViewModel = new TradesResultsViewModel(() =>
            {
                lock (_results.Results)
                {
                    return _results.Results.ToList();
                }
            });

            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(newResults =>
            {
                ResultsViewModel.UpdateResults();
            });

            _testResultsUpdatedObserver = _results.TestRunStarted.Subscribe(newResults =>
            {
                ResultsViewModel.UpdateResults();
            });

            Task.Run(() =>
            {
                Log.Info("Updating strategy run results");
                ResultsViewModel.UpdateResults();
                Log.Info("Updated strategy run results");
            });

            _uiService.RegisterF5Action(() =>
            {
                Save();
                RunStrategyClicked(null);
            }, true);

            _uiService.RegisterControlSAction(() =>
            {
                Save();
            }, true);

            /*_markets = new[]
            {
                "EUR/USD", "GBP/USD", "USD/JPY", "USD/CHF", "AUD/USD", "NZD/USD", "USD/CAD", "EUR/JPY",
                "EUR/AUD", "EUR/GBP", "EUR/CAD", "GBP/CAD", "AUD/CHF"
            };

            Task.Run(() =>
            {
                foreach (var market in _markets)
                {
                    Log.Info($"Loading candles {market} M1");
                    _candlesService.GetCandles(_broker, market, Timeframe.M1, false);
                }
                Log.Info($"Loaded candles");
            });*/
        }

        public ICommand RunStrategyCommand { get; private set; }
        public ICommand StopStrategyCommand { get; private set; }


        private void DeleteStrategy()
        {
            if (string.IsNullOrEmpty(SelectedStrategyFilename))
            {
                return;
            }

            var path = Path.Combine(_strategiesDirectory, $"{SelectedStrategyFilename}.txt");
            File.Delete(path);

            SelectedStrategyFilename = string.Empty;
            RefreshStrategyFilenames();
        }

        private void StopStrategyClicked(object obj)
        {
            Log.Info("Stopping simulation");
            _stopRun = true;
        }

        public bool RunStrategyEnabled
        {
            get { return _runStrategyEnabled; }
            private set
            {
                _runStrategyEnabled = value;
                NotifyPropertyChanged();
            }
        }

        public bool StopStrategyEnabled
        {
            get => _stopStrategyEnabled;
            set
            {
                _stopStrategyEnabled = value;
                NotifyPropertyChanged();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName]string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void RunStrategyClicked(object o)
        {
            Task.Run((Action)RunStrategy);
        }

        private void RunStrategy()
        {
            if (string.IsNullOrEmpty(SelectedStrategyFilename)) return;
            if (!RunStrategyEnabled) return;

            try
            {
                _dispatcher.Invoke(() =>
                {
                    RunStrategyEnabled = false;
                    StopStrategyEnabled = true;
                });

                var code = CodeText;
                byte[] hash = null;
                using (var sha256Hash = SHA256.Create())
                {
                    hash = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(code));
                }

                if (_strategyHash == null || !_strategyHash.SequenceEqual(hash))
                {
                    _strategyHash = hash;
                    Log.Info("Compiling strategy");

                    _strategyType = StrategyHelper.CompileStrategy2(code);
                    Log.Info("Compilation complete");
                }

                if (_strategyType == null)
                {
                    Log.Error("Failed to compile strategy");
                    _dispatcher.Invoke(() =>
                    {
                        RunStrategyEnabled = true;
                        StopStrategyEnabled = false;
                    });
                    return;
                }

                Log.Info("Running simulation");
                _stopRun = false;
                var stopwatch = Stopwatch.StartNew();

                _results.Reset();

                _dispatcher.Invoke((Action) (() =>
                {
                    _results.RaiseTestRunStarted();

                    NotifyPropertyChanged("TotalTrades");
                    NotifyPropertyChanged("AverageRTrade");
                    NotifyPropertyChanged("TotalR");
                    NotifyPropertyChanged("PercentSuccessTrades");
                    NotifyPropertyChanged("AverageWinningRRRTrades");
                    NotifyPropertyChanged("AverageLosingRRRTrades");

                    RunStrategyEnabled = false;
                    StopStrategyEnabled = true;
                }));

                var completed = 0;

                _producerConsumer = new ProducerConsumer<(Type StrategyType, MarketDetails Market)>(4, d =>
                {
                    if (_stopRun) return ProducerConsumerActionResult.Stop;
                    var strategyTester =
                        new StrategyRunner2(_candlesService, _marketDetailsService, _broker,
                            d.Market); //, _tradeCalculatorService, _marketDetailsService,
                    //_tradeCache,
                    //SimulationRunnerFlags.DoNotValidateStopsLimitsOrders);
                    var strategy = (StrategyBase2)Activator.CreateInstance(d.StrategyType);
                    if (strategy != null && strategy.Markets == null)
                    {
                        strategy.SetMarkets(StrategyBase2.Majors.Concat(StrategyBase2.Minors).Concat(StrategyBase2.MajorIndices).ToArray());
                    }

                    var result = strategyTester.Run(strategy, getShouldStopFunc: () => _stopRun, strategy.StartTime,
                        strategy.EndTime);
                    // tradesCompletedProgressFunc: tradesProgress =>
                    //{
                    //_results.AddResult(tradesProgress);
                    // });

                    if (result != null)
                    {
                        _results.AddResult(result);

                        // Adding trades to UI in realtime slows down the UI too much with strategies with many trades

                        completed++;
                        Log.Info($"Completed {completed}/{strategy.Markets.Length}");
                    }

                    return ProducerConsumerActionResult.Success;
                });

                var expectedTrades = new List<ExpectedTradeAttribute>();
                var strategy = (StrategyBase2)Activator.CreateInstance(_strategyType);

                if (strategy != null && strategy.Markets == null)
                {
                    strategy.SetMarkets(StrategyBase2.Majors.Concat(StrategyBase2.Minors).Concat(StrategyBase2.MajorIndices).ToArray());
                }

                foreach (var market in strategy.Markets)
                {
                    _producerConsumer.Add((_strategyType, _marketDetailsService.GetMarketDetails(_broker.Name, market)));
                     var expectedTradesFile = strategy.GetType().GetCustomAttribute<ExpectedTradesFileAttribute>();
                     if (expectedTradesFile != null)
                     {
                         var lines = File.ReadAllLines(expectedTradesFile.Path);
                         for (var i = 1; i < lines.Length; i++)
                         {
                             var line = lines[i];
                             var csv = line.Split(',');
                             var expectedTrade = new ExpectedTradeAttribute(
                                 csv[1],
                                 csv[0],
                                 csv[10],
                                  decimal.Parse(csv[6]),
                                 decimal.Parse(csv[9]),
                                 csv[2] == "Bullish" ? TradeDirection.Long : TradeDirection.Short);
                             expectedTrades.Add(expectedTrade);
                         }
                     }
                }

                _producerConsumer.Start();
                _producerConsumer.SetProducerCompleted();
                _producerConsumer.WaitUntilConsumersFinished();


                stopwatch.Stop();
                Log.Info($"Simulation run completed in {stopwatch.Elapsed.TotalSeconds}s");

                var trades = _results.Results.ToList();

                var matches = 0;
                foreach (var expectedTrade in expectedTrades.ToList())
                {
                    var matchedTrade = trades.FirstOrDefault(t =>
                        Math.Abs(t.EntryPrice.Value - expectedTrade.EntryPrice) < 0.01M
                        && (t.EntryDateTime == expectedTrade.OpenTimeUTC.AddHours(-2)
                            || t.EntryDateTime == expectedTrade.OpenTimeUTC.AddHours(1)));
                    if (matchedTrade != null)
                    {
                        trades.Remove(matchedTrade);
                        expectedTrades.Remove(expectedTrade);
                        matches++;
                    }
                }

                if (expectedTrades.Count > 0)
                {
                    foreach (var expectedTrade in expectedTrades)
                    {
                        Log.Info(
                            $"Not matched: Open:{expectedTrade.OpenTimeUTC} Entry:{expectedTrade.EntryPrice:0.000}");
                    }

                    Log.Info($"Matched {matches} trades. {trades.Count} additional trades. {expectedTrades.Count} not matched");
                }

                // Save results
                /*if (File.Exists(_savedResultsPath))
                {
                    File.Delete(_savedResultsPath);
                }
                File.WriteAllText(_savedResultsPath, JsonConvert.SerializeObject(_results.Results));*/


                // ==== Write ML learning data
                var outputPath = @"C:\OCW\SrcPythonAIForexTrader\Data\NewData.csv";
                var dataWriter = new MLDataWriter(_broker, _candlesService);
                var m = "GBP/USD";
                var tradesForMarket = trades.Where(t => t.Market == m).ToList();
                var tradesForLearning = new List<Trade>();
                tradesForLearning.AddRange(tradesForMarket.Where(t => t.RMultiple > 0).Take(1000));
                tradesForLearning.AddRange(tradesForMarket.Where(t => t.RMultiple < 0).Take(1000));
                tradesForLearning = tradesForLearning.OrderBy(t => t.OrderDateTime).ToList();
                //dataWriter.WriteMLData(tradesForLearning, m, outputPath, Timeframe.H2);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to run strategy", ex);
            }

            _dispatcher.Invoke((Action)(() =>
            {
                _results.RaiseTestRunCompleted();

                NotifyPropertyChanged("TotalTrades");
                NotifyPropertyChanged("AverageRTrade");
                NotifyPropertyChanged("TotalR");
                NotifyPropertyChanged("PercentSuccessTrades");
                NotifyPropertyChanged("AverageWinningRRRTrades");
                NotifyPropertyChanged("AverageLosingRRRTrades");

                RunStrategyEnabled = true;
                StopStrategyEnabled = false;
            }));
        }

        public void ViewClosing()
        {
            _producerConsumer?.Stop();
        }

        private void LoadDefaultStrategyText()
        {
            var binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _defaultStrategyText = File.ReadAllText(Path.Combine(binPath, "DefaultStrategy.cs"));
        }

        private void RefreshStrategyFilenames()
        {
            StrategyFilenames.Clear();
            var strategyPaths = Directory.GetFiles(_strategiesDirectory, "*.txt");
            foreach (var strategyPath in strategyPaths)
            {
                StrategyFilenames.Add(Path.GetFileNameWithoutExtension(strategyPath));
            }
        }

        public ObservableCollection<string> StrategyFilenames { get; } = new ObservableCollection<string>();

        private void CreateStrategy()
        {
            var res = InputView.Show();

            if (res.OKClicked && !string.IsNullOrEmpty(res.Text))
            {
                var path = Path.Combine(_strategiesDirectory, $"{res.Text}.txt");
                File.WriteAllText(path, _defaultStrategyText);

                RefreshStrategyFilenames();
            }
        }

        public TradesResultsViewModel ResultsViewModel { get; }

        public string SelectedStrategyFilename
        {
            get => _selectedStrategyFilename;
            set
            {
                Save();

                _selectedStrategyFilename = value;
                OnPropertyChanged();

                if (!string.IsNullOrEmpty(_selectedStrategyFilename))
                {
                    var code = File.ReadAllText(Path.Combine(_strategiesDirectory, $"{_selectedStrategyFilename}.txt"));
                    CodeText = code;
                }
                else
                {
                    CodeText = string.Empty;
                }
            }
        }

        private void Save()
        {
            if (!string.IsNullOrEmpty(SelectedStrategyFilename))
            {
                var path = Path.Combine(_strategiesDirectory, $"{SelectedStrategyFilename}.txt");

                if (File.Exists(path))
                {
                    File.WriteAllText(path, CodeText);
                    Log.Info("Saved");
                }
            }
        }

        public DelegateCommand SaveCommand { get; private set; }

        public DelegateCommand CreateStrategyCommand { get; private set; }

        public DelegateCommand DeleteStrategyCommand { get; private set; }

        public string CodeText
        {
            get => _codeText;
            set
            {
                _codeText = value;
                //StrategyRunViewModel.CustomCode = _codeText;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MLDataWriter
    {
        private readonly IBroker _broker;
        private readonly IBrokersCandlesService _candlesService;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int CandlesOfPrices = 10;
        private const int CandlesOfEma = 6;
        private const int CandlesT3 = 6;  // Normalise over larger range
        private const int CandlesAO = 6; // Normalise over larger range
        private const int CandlesSpread = 5;
        
        public MLDataWriter(IBroker broker, IBrokersCandlesService candlesService)
        {
            _broker = broker;
            _candlesService = candlesService;
        }

        public void WriteMLData(List<Trade> trades, string market, string outputDataPath, Timeframe timeframe)
        {
            try
            {
               /* foreach (var t in trades)
                {
                    if (string.IsNullOrEmpty(t.CustomText1))
                    {
                        t.CustomText1 = "OneStrategy";
                    }
                }

                // Organise trades by order date/time
                var tradesByDateTime = new Dictionary<DateTime, List<Trade>>();
                foreach (var t in trades.Where(x => x.OrderDateTime != null || x.EntryDateTime != null))
                {
                    var d = t.OrderDateTime ?? t.EntryDateTime;
                    if (!tradesByDateTime.ContainsKey(d.Value))
                        tradesByDateTime.Add(d.Value, new List<Trade>());
                    tradesByDateTime[d.Value].Add(t);
                }

                var setups = trades.SelectMany(x => x.CustomText1.Split(',')).Distinct().OrderBy(a => a).ToList();

                var timeframeIndicators = new TimeframeLookup<Indicator[]>();
                timeframeIndicators.Add(timeframe,
                    new[]
                    {
                        Indicator.ATR, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.T3CCI, Indicator.AO
                    });
                var timeframesAllCandles = TimeframeLookupBasicCandleAndIndicators.PopulateCandles(_broker, market,
                    new[] { timeframe }, timeframeIndicators, _candlesService);
                var m1Candles = TimeframeLookupBasicCandleAndIndicators.GetM1Candles(_broker, market, _candlesService);

                var prices = new List<float>();
                var emas = new List<float>();
                var t3 = new List<float>();
                var ao = new List<float>();
                var spread = new List<float>(); // Spread should be normalised like prices to keep same scale

                var str = new StringBuilder();
                CreateHeader(str, CandlesOfPrices, CandlesSpread, CandlesOfEma, CandlesT3, CandlesAO, setups);
                var columnsCount = str.ToString().Split(',').Length + 1;

                float? t3Min = null, t3Max = null, aoMin = null, aoMax = null;
                TimeframeLookupBasicCandleAndIndicators.IterateThroughCandles(
                    timeframesAllCandles,
                    m1Candles,
                    n =>
                    {
                        if (!n.NewCandleFlags.HasFlag(NewCandleFlags.CompleteNonM1Candle)) return;
                        var c = n.CurrentCandles[timeframe][n.CurrentCandles[timeframe].Count - 1];

                        // Update all data (Un-normalised)
                        UpdateData(c, prices, spread, emas, t3, ao, ref t3Min, ref t3Max, ref aoMin, ref aoMax);

                        if (t3Min == null || t3Max == null || aoMin == null || aoMax == null) return;

                        if (!GetInputsForLine(prices, emas, spread, t3, ao, t3Min.Value, t3Max.Value, aoMin.Value,
                            aoMax.Value, out var p, out var e, out var s, out var t, out var a)) return;
                        if (!GetTradesForLine(tradesByDateTime, c, setups, out var bestTrade,
                            out var orderedTrades)) return;

                        var newLineStr = new StringBuilder();
                        AddLine(newLineStr, p, e, s, t, a, bestTrade, orderedTrades);
                        var lineColumnCount = newLineStr.ToString().Split(',').Length + 1;
                        if (lineColumnCount != columnsCount)
                        {
                            Log.Error($"Wrong number of columns for line - should have {columnsCount} columns");
                        }

                        str.Append(newLineStr);
                    },
                    d => $"Creating data {d.PercentComplete:0.00}%", () => false);

                // Check again for correct column count
                foreach (var line in str.ToString().Split('\n'))
                {
                    if (line.Split(',').Length + 1 != columnsCount)
                    {
                        Log.Error($"Wrong number of columns for line - should have {columnsCount} columns");
                    }
                }

                Log.Info($"Writing results to file {outputDataPath}");
                File.WriteAllText(outputDataPath, str.ToString());
                Log.Info("Done");*/
            }
            catch (Exception ex)
            {
                Log.Error("Unable to write ML data", ex);
            }
        }

        private static void CreateHeader(StringBuilder str, int candlesOfPrices, int candlesSpread, int candlesOfEma,
            int candlesT3, int candlesAO, List<string> setups)
        {
            for (var i = 0; i < candlesOfPrices; i++)
            {
                if (str.Length > 0) str.Append(",");
                str.Append($"Price Close {i + 1},");
                str.Append($"Price Open {i + 1},");
                str.Append($"Price High {i + 1},");
                str.Append($"Price Low {i + 1}");
            }

            for (var i = 0; i < candlesSpread; i++)
            {
                str.Append($",Spread {i + 1}");
            }

            for (var i = 0; i < candlesOfEma; i++)
            {
                str.Append($",EMA 8 {i + 1},EMA 25 {i + 1},EMA 50 {i + 1}");
            }

            for (var i = 0; i < candlesT3; i++)
            {
                str.Append($",T3 {i + 1}");
            }

            for (var i = 0; i < candlesAO; i++)
            {
                str.Append($",AO {i + 1}");
            }

            str.Append(",BestTradeSetup");

            foreach (var s in setups)
            {
                str.Append($",{s}");
            }
        }

        private static void UpdateData(CandleAndIndicators c, List<float> prices, List<float> spread, List<float> emas,
            List<float> t3, List<float> ao, ref float? t3Min, ref float? t3Max, ref float? aoMin, ref float? aoMax)
        {
            // Update prices and spread
            prices.AddRange(new[] { c.Candle.CloseBid, c.Candle.OpenBid, c.Candle.HighBid, c.Candle.LowBid });
            spread.Add(Math.Abs(c.Candle.HighBid - c.Candle.HighAsk));

            // Update EMAs
            if (c[Indicator.EMA8].IsFormed && c[Indicator.EMA25].IsFormed && c[Indicator.EMA50].IsFormed)
            {
                emas.AddRange(new[] { c[Indicator.EMA8].Value, c[Indicator.EMA25].Value, c[Indicator.EMA50].Value });
            }

            // Update other indicators
            if (c[Indicator.T3CCI].IsFormed)
            {
                var v = c[Indicator.T3CCI].Value;
                t3.Add(v);
                if (t3Min == null || v < t3Min) t3Min = v;
                if (t3Max == null || v > t3Min) t3Max = v;
            }

            if (c[Indicator.AO].IsFormed)
            {
                var v = c[Indicator.AO].Value;
                ao.Add(v);
                if (aoMin == null || v < aoMin) aoMin = v;
                if (aoMax == null || v > aoMax) aoMax = v;
            }
        }
        private static bool GetInputsForLine(
            List<float> prices, List<float> emas, List<float> spread, List<float> t3, List<float> ao,
            float t3Min, float t3Max, float aoMin, float aoMax,
            out List<float> p, out List<float> e, out List<float> s, out List<float> t, out List<float> a)
        {
            p = null;
            e = null;
            s = null;
            t = null;
            a = null;
            if (prices.Count < CandlesOfPrices * 4 || emas.Count < CandlesOfEma * 3) return false;

            // Get prices and EMAs and normalise together
            p = prices.TakeLast(CandlesOfPrices * 4).ToList();
            e = emas.TakeLast(CandlesOfEma * 3).ToList();
            var m = Normalise(p, e);

            // Get spreads - this should be normalised using just the range - the positions don't need to be adjusted
            s = spread.TakeLast(5).ToList();
            s = s.Select(x => x / (m.Max - m.Min)).ToList();

            // T3 should have the same range over whole time period - use that for normalising
            t = t3.TakeLast(CandlesT3).Select(x => (x - t3Min) / (t3Max - t3Min)).ToList();

            // AO should have the same range over whole time period - use that for normalising
            a = ao.TakeLast(CandlesAO).Select(x => (x - aoMin) / (aoMax - aoMin)).ToList();
            return true;
        }

        private static (float Min, float Max) Normalise(params List<float>[] lists)
        {
            var min = lists.SelectMany(x => x).Min();
            var max = lists.SelectMany(x => x).Max();
            var ratio = 1.0F / (max - min);
            foreach (var list in lists)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    list[i] = (list[i] - min) * ratio;
                }
            }

            return (min, max);
        }

        private static bool GetTradesForLine(Dictionary<DateTime, List<Trade>> tradesByDateTime, CandleAndIndicators c, List<string> setups,
            out Trade bestTrade, out List<Trade> orderedTrades)
        {
            bestTrade = null;
            orderedTrades = null;

            // Get trades
            tradesByDateTime.TryGetValue(c.Candle.CloseTime(), out var trades);
           // if (trades == null || trades.Count(x => x.RMultiple != null) < (setups.Count * 0.1)) return false; // Need at least 10% of the trades for setups

            // First, get best trade
            bestTrade = trades.Where(x => x.RMultiple != null).OrderByDescending(x => x.RMultiple).FirstOrDefault();

            // Get all trades in correct order
            orderedTrades = new List<Trade>();
            foreach (var setup in setups)
            {
                var t = trades.FirstOrDefault(x => x.CustomText1 == setup);
                orderedTrades.Add(t);
            }

            return true;
        }

        private static void AddLine(StringBuilder str, List<float> p, List<float> e, List<float> s, List<float> t, List<float> a, Trade bestTrade,
            List<Trade> orderedTrades)
        {
            str.AppendLine(string.Empty); // STart new line

            // Write prices
            var first = true;
            foreach (var x in p)
            {
                if (!first) str.Append(",");
                first = false;
                str.Append($"{x:0.0000}");
            }

            foreach (var x in e) str.Append($",{x:0.0000}"); // Write EMAs
            foreach (var x in s) str.Append($",{x:0.0000}"); // Write spreads
            foreach (var x in t) str.Append($",{x:0.0000}"); // Write T3s
            foreach (var x in a) str.Append($",{x:0.0000}"); // Write AOs

            // Write best setup
            str.Append($",{bestTrade?.CustomText1}");

            foreach (var trade in orderedTrades)
            {
                str.Append(trade != null ? $",{trade.RMultiple:0.0000}" : ",");
            }
        }
    }
}