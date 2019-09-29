using System;
using System.Collections.Generic;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Simulation;
using TraderTools.AI;

namespace AutomatedTraderDesigner
{
    [RequiredTimeframeCandles(Timeframe.D1, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    public class RandomTradesWithTrend : StrategyBase
    {
        private DataGenerator _dataGenerator;
        public override string Name => "Random Trades With Trend";
        private Random _rnd = new Random();
        private IModelDetails _model;

        public RandomTradesWithTrend()
        {
            _dataGenerator = new DataGenerator();
        }

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<CandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService)
        {
            if (_model == null) _model = LoadModel("Trend2");

            var candles = candlesLookup[Timeframe.D1];
            if (candles.Count < 20) return null;

            var c = candles[candles.Count - 1];
            var atr = c[Indicator.ATR].Value;

            var rndNumber = _rnd.Next(1, 100);

            if ((rndNumber >= 60))
            {
                _dataGenerator.CreateRawData(candlesLookup[Timeframe.D1], candlesLookup[Timeframe.D1].Count - 1, _model, out var rawData);
                if (rawData.Length == 0)
                {

                }

                var y = Predict(_model, rawData);

                var trendUp = 1;
                var trendDown = 0;
                if (y == trendUp)
                {
                    var entryPrice = c[Indicator.EMA8].Value;
                    var stop = entryPrice - (atr * 1.0);
                    var limit = entryPrice + (atr * 1.15);
                    // var t = CreateMarketOrder(market.Name, TradeDirection.Long, c.Candle, (decimal)stop, 0.005M, (decimal)limit);
                    var t = CreateOrder(market.Name, c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 1), (decimal)entryPrice, TradeDirection.Long, (decimal?)limit, (decimal)stop, 0.005M);
                    return new List<Trade> { t };
                }

                if (y == trendDown)
                {
                    var entryPrice = c[Indicator.EMA8].Value;
                    var stop = entryPrice + (atr * 1.0);
                    var limit = entryPrice - (atr * 1.15);
                    var t = CreateOrder(market.Name, c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 1), (decimal)entryPrice, TradeDirection.Short, (decimal?)limit, (decimal)stop, 0.005M);
                    return new List<Trade> { t };
                }
            }

            return null;
        }
    }
}