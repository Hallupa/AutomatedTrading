using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using log4net;
using Numpy;
using StrategyEditor.ViewModels;
using TraderTools.Basics;

/* TODOusing Tensorflow;
using static Tensorflow.KerasApi;
using static Tensorflow.Binding;
using Tensorflow.Keras;
using Tensorflow.Keras.ArgsDefinition;
using Tensorflow.Keras.Engine;
using Tensorflow.Keras.Layers;
using Tensorflow.NumPy;
using TraderTools.Basics;*/

namespace StrategyEditor.ML
{
    public class MLFoundPoint
    {
        public MLFoundPoint(long dateTime, TradeDirection direction, decimal price)
        {
            DateTime = dateTime;
            Direction = direction;
            Price = price;
        }

        public long DateTime { get; }
        public TradeDirection Direction { get; }
        public decimal Price { get; }
    }

    public class Trainer
    {
        private IBrokersCandlesService _candlesService;
        private readonly IBrokersService _brokersService;
        // TODO private Sequential _model;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private TaskScheduler _modelThreadContext;
        private Dispatcher _modelDispatcher;
        private DataGenerator _dataGenerator;

        static Trainer()
        {
            /* TODO Tensorflow.ops.enforce_singlethreading();
            Binding.tf_output_redirect = new TensorFlowTextWriter();*/
        }

        public Trainer(IBrokersCandlesService candlesService, IBrokersService brokersService)
        {
            _candlesService = candlesService;
            _brokersService = brokersService;

            _dataGenerator = new DataGenerator(_candlesService, _brokersService);
            ManualResetEvent dispatcherReadyEvent = new ManualResetEvent(false);

            new Thread(new ThreadStart(() =>
            {
                //      if (!tf.executing_eagerly())
              //           tf.enable_eager_execution();
               // tf.Context.ensure_initialized();

                _modelDispatcher = Dispatcher.CurrentDispatcher;
                dispatcherReadyEvent.Set();
                Dispatcher.Run();
            })).Start();

            dispatcherReadyEvent.WaitOne();
        }

        public Task<List<MLFoundPoint>> TestAsync(List<Candle> candles)
        {
            return Task.Run(() =>
            {
                if (Dispatcher.CurrentDispatcher == _modelDispatcher)
                {
                    return Test(candles);
                }
                else
                {
                    List<MLFoundPoint> ret = null;
                    _modelDispatcher.Invoke(() =>
                    {
                        ret = Test(candles);
                    });

                    return ret;
                }
            });
        }

        private List<MLFoundPoint> Test(List<Candle> candles)
        {
            return null;
            /* TODO Log.Info("Running test");
            try
            {
                var ret = new List<MLFoundPoint>();
                var x = new List<float>();
                var xpoints = 0;
                for (var i = 20; i < candles.Count; i++)
                {
                    var z = _dataGenerator.GetMLPointXData(candles, i);
                    if (xpoints == 0) xpoints = z.Count;

                    x.AddRange(z);
                }

                var inX = np.array(x.ToArray());
                var countIn = (int)(x.Count / (decimal)xpoints);

                inX = inX.reshape(new int[] { countIn, xpoints });

                var outY = _model.predict(inX, countIn);
                for (var i = 0; i < countIn; i++)
                {
                    var values = outY[0][i].ToArray<float>();
                    if (values.Any(v => v > 1F || v < 0F))
                    {
                        throw new ApplicationException("Test values is <0 or >1");
                    }

                    var buy = values[0];
                    var sell = values[1];

                    if (buy > 0.95F) ret.Add(new MLFoundPoint(candles[i].CloseTimeTicks, TradeDirection.Long, (decimal)candles[i].CloseBid));
                    if (sell > 0.95F) ret.Add(new MLFoundPoint(candles[i].CloseTimeTicks, TradeDirection.Short, (decimal)candles[i].CloseBid));
                }

                Log.Info($"Test complete - {ret.Count} points found");
                return ret;
            }
            catch (Exception ex)
            {
                Log.Error("Failing to run test", ex);
                return null;
            }*/
        }

        public Task TrainAsync(MLPointCollection points)
        {
            return Task.Factory.StartNew(() =>
            {
                /* TODO _modelDispatcher.Invoke(() =>
                {
                    tf.Context.ensure_initialized();
                    Log.Info("Training");
                    var xydata = _dataGenerator.GetPointsXYData(points);

                    var x = np.array(xydata.x.ToArray());
                    var y = np.array(xydata.y.ToArray());
                    var count = (int)(xydata.x.Count / (decimal)xydata.dataItemsPerX);
                    x = x.reshape(new int[] { count, xydata.dataItemsPerX });
                    y = y.reshape(new int[] { count, 3 });

                    //Tensorflow.InvalidArgumentError: 'In[0] mismatch In[1] shape: 28 vs. 1120: [5,28] [1120,60] 0 0'
                    /*_model = keras.Sequential(
                        new List<ILayer>
                        {
                            new Flatten(new FlattenArgs
                            {
                                InputShape = new Shape(new [] { xydata.dataItemsPerX })
                            }),
                            //keras.layers.Flatten(),
                            keras.layers.Dense(xydata.dataItemsPerX, activation: "relu"),//, input_shape: new TensorShape(-1, xydata.dataItemsPerX)),
                            keras.layers.Dense(60, activation: "relu"),
                            keras.layers.Dense(40, activation: "relu"),
                            keras.layers.Dense(3, activation: "softmax"),
                        });

                    _model.compile(keras.optimizers.SGD(0.01F), keras.losses.CategoricalCrossentropy(from_logits: true));*/
                    

                    /*   CategoricalCrossentropy /
                    
                    var numberOfClasses = 3;
                    _model = keras.Sequential(
                        new List<ILayer>
                        {
                            new Flatten(new FlattenArgs
                            {
                                InputShape = new Shape(xydata.dataItemsPerX)
                            }),
                            //keras.layers.Flatten(),
                            keras.layers.Dense(xydata.dataItemsPerX, activation: "relu"),//, input_shape: new TensorShape(-1, xydata.dataItemsPerX)),
                            keras.layers.Dropout(0.2F),
                            keras.layers.Dense(12, activation: "relu"),
                            keras.layers.Dropout(0.2F),
                            keras.layers.Dense(6, activation: "relu"),
                            keras.layers.Dense(numberOfClasses, activation: "softmax"),
                        });

                    //var loss = new SGD(0.05F);
                    //var optimiser = new SparseCategoricalCrossentropy();
                    //model.compile(loss, optimiser, new[] { "accuracy" });
                    //model.compile(new SGD(0.1F), new SparseCategoricalCrossentropy(), new[] { "accuracy" });

                    // logits and labels must have the same first dimension, got logits shape [5,3] and labels shape [15]'
                    _model.compile(
                        keras.optimizers.Adam(0.01F),
                        keras.losses.CategoricalCrossentropy(),
                        new[] { "acc" });

                    //here // SparseCategoricalCrossentropy?  Validation set? More generated data?/

                    _model.fit(x, y, 5, 100, 1, validation_split: 0.1F);
                    Log.Info("Training complete");/
                });*/
            });//, TaskCreationOptions.LongRunning);
        }
    }
}