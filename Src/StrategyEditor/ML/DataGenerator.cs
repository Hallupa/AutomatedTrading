using System;
using System.Collections.Generic;
using System.Linq;
using Hallupa.Library.Extensions;
using StrategyEditor.ViewModels;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace StrategyEditor.ML
{
    public class DataGenerator
    {
        private readonly IBrokersCandlesService _candlesService;
        private readonly IBrokersService _brokersService;
        private const int CandlesCountForDataPoint = 6;

        public DataGenerator(IBrokersCandlesService candlesService, IBrokersService brokersService)
        {
            _candlesService = candlesService;
            _brokersService = brokersService;
        }

        public (List<float> x, List<float> y, List<Candle> candlesUsed) GetPointXYData(MLPoint p, MLPointCollection points)
        {
            var candlesLookup = GetCandlesLookup(points);
            return GetPointXYData(p, candlesLookup);
        }

        public (List<float> x, List<float> y, List<Candle> candlesUsed) GetPointXYData(
                MLPoint p,
                Dictionary<(string Market, Timeframe Timeframe),
                List<Candle>> candlesLookup)
        {
            var allCandles = candlesLookup[(p.Market, p.Timeframe)];
            var index = allCandles.BinarySearchGetItem(i => allCandles[i].CloseTimeTicks, 0,
                p.DateTimeUtc.Ticks, BinarySearchMethod.PrevLowerValueOrValue);
            var currentCandles = allCandles
                .GetRange(index - CandlesCountForDataPoint + 1, CandlesCountForDataPoint).ToList();

            var x = GetMLPointXData(currentCandles);
            return (
                x,
                new List<float>
                {
                    p.PointType == MLPointType.Buy ? 1.0F : 0.0F, p.PointType == MLPointType.Sell ? 1.0F : 0.0F,
                    p.PointType == MLPointType.Hold ? 1.0F : 0.0F
                },
                currentCandles);
        }

        public (List<float> x, List<float> y, int dataItemsPerX) GetPointsXYData(MLPointCollection points)
        {
            var candlesLookup = GetCandlesLookup(points);
            var rnd = new Random();

            var xValues = new List<float>();
            var yValues = new List<float>();

            var xpoints = 0;
            foreach (var p in points.Points)
            {
                var xy = GetPointXYData(p, candlesLookup);
                if (xpoints == 0) xpoints = xy.x.Count;


                xValues.AddRange(xy.x);
                yValues.AddRange(xy.y);

                if (points.GenerateExtraPoints)
                {
                    for (var x = xy.candlesUsed[0].OpenBid * 0.001F;
                        x < xy.candlesUsed[0].OpenBid * 0.003F;
                        x += xy.candlesUsed[0].OpenBid * 0.0005F)
                    {
                        var gcandles = new List<Candle>();
                        var add = xy.candlesUsed[0].OpenBid * 0.001F;
                        for (var i = 0; i < xy.candlesUsed.Count; i++)
                        {
                            var c = xy.candlesUsed[i];
                            var gc = new Candle
                            {
                                OpenBid = c.OpenBid + add,
                                HighBid = c.HighBid + add,
                                CloseBid = c.CloseBid + add,
                                LowBid = c.LowBid + add
                            };
                            gcandles.Add(gc);

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
            }

            return (xValues, yValues, xpoints);
        }

        public List<float> GetMLPointXData(List<Candle> allCandles, int uptoIndexInclusive = -1)
        {
            var candles =
                uptoIndexInclusive != -1
                    ? allCandles.GetRange(uptoIndexInclusive - CandlesCountForDataPoint + 1, CandlesCountForDataPoint)
                    : allCandles.GetRange(allCandles.Count - CandlesCountForDataPoint, CandlesCountForDataPoint);
            var maxPrice = candles.Select(x => x.HighBid).Max();
            var minPrice = candles.Select(x => x.LowBid).Min();
            var priceRange = maxPrice - minPrice;

            // Attempt 1: open prices, close prices
            /*var ret = new List<float>();
            foreach (var c in candles)
            {
                ret.append((c.OpenBid - minPrice) / priceRange);
                ret.append((c.CloseBid - minPrice) / priceRange);
                ret.append((c.HighBid - minPrice) / priceRange);
                ret.append((c.LowBid - minPrice) / priceRange);
            }*/

            // Attempt 2: candle body sizes
            var ret = new List<float>();

            // Up candle bodies
            foreach (var c in candles)
            {
                if (c.CloseBid > c.OpenBid)
                    ret.Add((c.CloseBid - c.OpenBid) / priceRange);
                else
                {
                    ret.Add(0.0F);
                }
            }

            // Down candle bodies
            foreach (var c in candles)
            {
                if (c.CloseBid < c.OpenBid)
                    ret.Add((c.OpenBid - c.CloseBid) / priceRange);
                else
                {
                    ret.Add(0.0F);
                }
            }

            if (ret.Any(zz => zz < 0F || zz > 1.0F)) throw new ApplicationException("Training data invalid");

            return ret;
        }

        private Dictionary<(string Market, Timeframe Tf), List<Candle>> GetCandlesLookup(MLPointCollection p)
        {
            return GetCandlesLookup(
                p.Points.Select(x => (x.Market, x.Timeframe)).Distinct().ToList(),
                p.Broker,
                p.UseHeikenAshi);
        }

        private Dictionary<(string Market, Timeframe Tf), List<Candle>> GetCandlesLookup(
            List<(string Market, Timeframe Timeframe)> marketsAndTimeframes,
            string broker,
            bool useHeikenAshi)
        {
            var lookup = new Dictionary<(string Market, Timeframe Tf), List<Candle>>();
            foreach (var x in marketsAndTimeframes.Distinct())
            {
                var candles = _candlesService.GetCandles(
                    _brokersService.GetBroker(broker),
                    x.Market,
                    x.Timeframe,
                    false);

                if (useHeikenAshi) candles = candles.CreateHeikinAshiCandles();

                lookup[(x.Market, x.Timeframe)] = candles;
            }

            return lookup;
        }
    }
}