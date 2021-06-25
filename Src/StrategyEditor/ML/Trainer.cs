using System;
using System.Collections.Generic;
using System.Linq;
using Hallupa.Library.Extensions;
using NumSharp;
using StrategyEditor.ViewModels;
using Tensorflow;
using static Tensorflow.KerasApi;
using NumSharp;
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

        static Trainer()
        {
            Binding.tf_output_redirect = new TensorFlowTextWriter();
        }

        public Trainer(IBrokersCandlesService candlesService, IBrokersService brokersService)
        {
            _candlesService = candlesService;
            _brokersService = brokersService;
        }

        private List<float> GetMLPointXData(long dateTimeTicks, List<Candle> allCandles)
        {
            var index = allCandles.BinarySearchGetItem(i => allCandles[i].CloseTimeTicks, 0, dateTimeTicks, BinarySearchMethod.PrevLowerValueOrValue);
            var count = 10;
            var candles = allCandles.GetRange(index - count + 1, count).ToList();
            var maxPrice = candles.Select(x => x.HighBid).Max();
            var minPrice = candles.Select(x => x.HighBid).Min();
            var priceRange = maxPrice - minPrice;

            // Attempt 1: open prices, close prices
            var ret = new List<float>();
            foreach (var c in candles)
            {
                ret.append((c.OpenBid - minPrice) / priceRange);
                ret.append((c.CloseBid - minPrice) / priceRange);
            }

            return ret;
        }

        public List<(long, TradeDirection)> Test(List<Candle> candles)
        {
            var ret = new List<(long, TradeDirection)>();
            var x = new List<float>();
            var xpoints = 0;
            for (var i = 20; i < candles.Count; i++)
            {
                var z = GetMLPointXData(candles[i].CloseTimeTicks, candles);
                if (xpoints == 0) xpoints = z.Count;

                x.AddRange(z);
            }

            var inX = np.array(x.ToArray());
            var countIn = (int)(x.Count / (decimal)xpoints);
            inX = inX.reshape(countIn, xpoints);

            var outY = _model.predict(inX, countIn);
            for (var i = 0; i < countIn; i++)
            {
                var buy = (float)outY[0][i][0].numpy().T;
                var sell = (float)outY[0][i][1].numpy().T;

                if (buy > 0.5F) ret.Add((candles[i].CloseTimeTicks, TradeDirection.Long));
                if (sell > 0.5F) ret.Add((candles[i].CloseTimeTicks, TradeDirection.Short));
            }

            return ret;
        }

        public void Train(MLPointCollection points)
        {
            var candlesLookup = GetCandlesLookup(points);

            var xValues = new List<float>();
            var yValues = new List<float>();

            var xpoints = 0;
            foreach (var p in points.Points)
            {
                var allCandles = candlesLookup[(p.Market, p.Timeframe)];
                var z = GetMLPointXData(p.DateTimeUtc.Ticks, allCandles);
                if (xpoints == 0) xpoints = z.Count;

                xValues.AddRange(z);
                yValues.AddRange(new float[]
                {
                    p.PointType == MLPointType.Buy ? 1.0F : 0.0F,
                    p.PointType == MLPointType.Sell ? 1.0F : 0.0F,
                    p.PointType == MLPointType.Hold ? 1.0F : 0.0F
                });
            }

            var x = np.array(xValues.ToArray());
            var y = np.array(yValues.ToArray());
            var count = (int)(xValues.Count / (decimal)xpoints);
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
            _model.compile(keras.optimizers.RMSprop(1e-3f), keras.losses.CategoricalCrossentropy(from_logits: true),
                new[] { "acc" });

            _model.fit(x, y, 3, 100, 1);
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