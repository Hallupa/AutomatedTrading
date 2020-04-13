using System;
using System.Collections.Generic;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Simulation;

namespace AutomatedTraderDesigner
{
    //[StopTrailIndicator(Timeframe.D1, Indicator.EMA25)]
    [RequiredTimeframeCandles(Timeframe.D1, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    [RequiredTimeframeCandles(Timeframe.H4, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    public class RandomTradesWithTrend : StrategyBase
    {
       // private DataGenerator _dataGenerator;
        public override string Name => "TradeTestAI";
        private Random _rnd = new Random();
        private IModelDetails _model;

        public RandomTradesWithTrend()
        {
           // _dataGenerator = new DataGenerator();
        }

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<CandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService, DateTime currentTime)
        {
            if (_model == null) _model = LoadModel("TraderTest2");

            var candles = candlesLookup[Timeframe.H4];
            if (candles.Count < 40) return null;
            if (candlesLookup[Timeframe.D1].Count < 40) return null;

            var c = candles[candles.Count - 1];
            var atr = c[Indicator.ATR].Value;

            var rndNumber = _rnd.Next(1, 100);

            //if ((rndNumber >= 60))
           /* {
                _dataGenerator.CreateRawData(candlesLookup[Timeframe.D1], candlesLookup[Timeframe.D1].Count - 1, _model, out var rawData);
                var y = Predict(_model, rawData);

                var trendUp = 1;
                var trendDown = 0;
                if (y == trendUp && c.Candle.Colour() == CandleColour.White)
                {
                    var entryPrice = c.Candle.CloseAsk;//c[Indicator.EMA25].Value;
                    var stop = entryPrice - (atr * 1.0);
                    var limit = (decimal?)null;
                    //var limit = entryPrice + (atr * 1.8);
                    var t = CreateMarketOrder(market.Name, TradeDirection.Long, c.Candle, (decimal)stop, 0.005M, (decimal?)limit);
                    //var t = CreateOrder(market.Name, c.Candle, c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 2), (decimal)entryPrice, TradeDirection.Long, (decimal?)limit, (decimal)stop, 0.005M);
                    return new List<Trade> { t };
                }

                if (y == trendDown && c.Candle.Colour() == CandleColour.Black)
                {
                    var entryPrice = c.Candle.CloseBid;// c[Indicator.EMA25].Value;
                    var stop = entryPrice + (atr * 1.0);
                    var limit = (decimal?)null;
                    //var limit = entryPrice - (atr * 1.8);
                    var t = CreateMarketOrder(market.Name, TradeDirection.Short, c.Candle, (decimal)stop, 0.005M, (decimal?)limit);
                    //var t = CreateOrder(market.Name, c.Candle, c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 2), (decimal)entryPrice, TradeDirection.Short, (decimal?)limit, (decimal)stop, 0.005M);
                    return new List<Trade> { t };
                }
            }*/

            return null;
        }
    }
}