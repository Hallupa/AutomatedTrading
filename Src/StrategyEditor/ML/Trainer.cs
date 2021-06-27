using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Hallupa.Library.Extensions;
using log4net;
using NumSharp;
using StrategyEditor.ViewModels;
using Tensorflow;
using static Tensorflow.KerasApi;
using Tensorflow.Keras;
using Tensorflow.Keras.ArgsDefinition;
using Tensorflow.Keras.Engine;
using Tensorflow.Keras.Layers;
using Tensorflow.Keras.Losses;
using Tensorflow.Keras.Optimizers;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace StrategyEditor.ML
{
    public class Trainer
    {
        private IBrokersCandlesService _candlesService;
        private readonly IBrokersService _brokersService;
        private Sequential _model;
        private const int CandlesCountForDataPoint = 10;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private TaskScheduler _modelThreadContext;
        private Dispatcher _modelDispatcher;

        static Trainer()
        {
            Tensorflow.ops.enforce_singlethreading();
            Binding.tf_output_redirect = new TensorFlowTextWriter();
        }

        public Trainer(IBrokersCandlesService candlesService, IBrokersService brokersService)
        {
            _candlesService = candlesService;
            _brokersService = brokersService;

            ManualResetEvent dispatcherReadyEvent = new ManualResetEvent(false);

            new Thread(new ThreadStart(() =>
            {
                _modelDispatcher = Dispatcher.CurrentDispatcher;
                dispatcherReadyEvent.Set();
                Dispatcher.Run();
            })).Start();

            dispatcherReadyEvent.WaitOne();
        }

        private List<float> GetMLPointXData(List<Candle> candles)
        {
            var maxPrice = candles.Select(x => x.HighBid).Max();
            var minPrice = candles.Select(x => x.LowBid).Min();
            var priceRange = maxPrice - minPrice;

            // Attempt 1: open prices, close prices
            var ret = new List<float>();
            foreach (var c in candles)
            {
                ret.append((c.OpenBid - minPrice) / priceRange);
                ret.append((c.CloseBid - minPrice) / priceRange);
                ret.append((c.HighBid - minPrice) / priceRange);
                ret.append((c.LowBid - minPrice) / priceRange);
            }

            return ret;
        }

        public Task<List<(long, TradeDirection)>> TestAsync(List<Candle> candles)
        {
            return Task.Run(() =>
            {
                if (Dispatcher.CurrentDispatcher == _modelDispatcher)
                {
                    return Test(candles);
                }
                else
                {
                    List<(long, TradeDirection)> ret = null;
                    _modelDispatcher.Invoke(() =>
                    {
                        ret = Test(candles);
                    });

                    return ret;
                }
            });
        }

        private List<(long, TradeDirection)> Test(List<Candle> candles)
        {
            Log.Info("Running test");
            try
            {
                var ret = new List<(long, TradeDirection)>();
                var x = new List<float>();
                var xpoints = 0;
                for (var i = 20; i < candles.Count; i++)
                {
                    var currentCandles = candles
                        .GetRange(i - CandlesCountForDataPoint + 1, CandlesCountForDataPoint)
                        .ToList();

                    var z = GetMLPointXData(currentCandles);
                    if (xpoints == 0) xpoints = z.Count;

                    x.AddRange(z);
                    break;
                }

                var inX = np.array(x.ToArray());
                var countIn = (int) (x.Count / (decimal) xpoints);
                inX = inX.reshape(countIn, xpoints);

                var outY = _model.predict(inX, countIn);
                for (var i = 0; i < countIn; i++)
                {
                    var buy = (float) outY[0][i][0].numpy().T;
                    var sell = (float) outY[0][i][1].numpy().T;

                    if (buy > 0.8F) ret.Add((candles[i].CloseTimeTicks, TradeDirection.Long));
                    if (sell > 0.8F) ret.Add((candles[i].CloseTimeTicks, TradeDirection.Short));
                    var t = outY[0][i][0].numpy().T + outY[0][i][1].numpy().T + outY[0][i][2].numpy().T;
                }

                Log.Info("Test complete");
                return ret;
            }
            catch (Exception ex)
            {
                Log.Error("Failing to run test", ex);
                return null;
            }
        }

        public Task TrainAsync(MLPointCollection points)
        {
            return Task.Factory.StartNew(() =>
            {
                _modelDispatcher.Invoke(() =>
                {
                    Log.Info("Training");
                    var candlesLookup = GetCandlesLookup(points);
                    var rnd = new Random();

                    var xValues = new List<float>();
                    var yValues = new List<float>();

                    var xpoints = 0;
                    foreach (var p in points.Points)
                    {
                        var allCandles = candlesLookup[(p.Market, p.Timeframe)];
                        var index = allCandles.BinarySearchGetItem(i => allCandles[i].CloseTimeTicks, 0,
                            p.DateTimeUtc.Ticks, BinarySearchMethod.PrevLowerValueOrValue);
                        var currentCandles = allCandles
                            .GetRange(index - CandlesCountForDataPoint + 1, CandlesCountForDataPoint).ToList();

                        var z = GetMLPointXData(currentCandles);
                        if (xpoints == 0) xpoints = z.Count;

                        xValues.AddRange(z);
                        yValues.AddRange(new[]
                        {
                            p.PointType == MLPointType.Buy ? 1.0F : 0.0F, p.PointType == MLPointType.Sell ? 1.0F : 0.0F,
                            p.PointType == MLPointType.Hold ? 1.0F : 0.0F
                        });

                        if (points.GenerateExtraPoints)
                        {
                            var gcandles = new List<Candle>();
                            var add = 0F;
                            for (var i = 0; i < currentCandles.Count; i++)
                            {
                                var c = currentCandles[i];
                                var gc = new Candle
                                {
                                    OpenBid = c.OpenBid + add,
                                    HighBid = c.HighBid + add,
                                    CloseBid = c.CloseBid + add,
                                    LowBid = c.LowBid + add
                                };
                                gcandles.add(gc);

                                add = add + c.OpenBid * 0.001F;
                            }

                            xValues.AddRange(GetMLPointXData(gcandles));
                            yValues.AddRange(new[]
                            {
                                p.PointType == MLPointType.Buy ? 1.0F : 0.0F,
                                p.PointType == MLPointType.Sell ? 1.0F : 0.0F,
                                p.PointType == MLPointType.Hold ? 1.0F : 0.0F
                            });
                        }
                    }

                    var x = np.array(xValues.ToArray());
                    var y = np.array(yValues.ToArray());
                    var count = (int) (xValues.Count / (decimal) xpoints);
                    x = x.reshape(count, xpoints);
                    y = y.reshape(count, 3);

                    _model = keras.Sequential(
                        new List<ILayer>
                        {
                            new Flatten(new FlattenArgs
                            {
                                BatchInputShape = new TensorShape(count, xpoints)
                            }),
                            keras.layers.Dense(40, activation: "relu"),
                            keras.layers.Dense(30, activation: "relu"),
                            keras.layers.Dense(3, activation: "softmax"),
                        });

                    var loss = new SGD(0.1F);
                    var optimiser = new SparseCategoricalCrossentropy();
                    //model.compile(loss, optimiser, new[] { "accuracy" });
                    //model.compile(new SGD(0.1F), new SparseCategoricalCrossentropy(), new[] { "accuracy" });
                    _model.compile(keras.optimizers.SGD(0.01F), keras.losses.CategoricalCrossentropy(from_logits: true),
                        new[] {"acc"});

                    //here // SparseCategoricalCrossentropy?  Validation set? More generated data?

                    _model.fit(x, y, 3, 100, 1, validation_split: 0F);
                    Log.Info("Training complete");
                });
            });//, TaskCreationOptions.LongRunning);
        }

        private Dictionary<(string Market, Timeframe Tf), List<Candle>> GetCandlesLookup(MLPointCollection p)
        {
            var lookup = new Dictionary<(string Market, Timeframe Tf), List<Candle>>();
            foreach (var x in p.Points.Select(x => (x.Market, x.Timeframe)).Distinct())
            {
                var candles = _candlesService.GetCandles(
                    _brokersService.GetBroker(p.Broker),
                    x.Market,
                    x.Timeframe,
                    false);

                if (p.UseHeikenAshi) candles = candles.CreateHeikinAshiCandles();

                lookup[(x.Market, x.Timeframe)] = candles;
            }

            return lookup;
        }
    }
}