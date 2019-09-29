using System.Collections.Generic;
using Keras.Models;
using Numpy;
using Numpy.Models;
using TraderTools.AI;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Trading;
using TraderTools.Simulation;

namespace AutomatedTraderDesigner
{
    [StopTrailIndicator(Timeframe.H2, Indicator.EMA8)]
    [RequiredTimeframeCandles(Timeframe.H2, Indicator.EMA8)]
    [RequiredTimeframeCandles(Timeframe.D1, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    [RequiredTimeframeCandles(Timeframe.H4, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    public class NewStrategy : StrategyBase
    {
        private DataGenerator _dataGenerator;
        public override string Name => "Name";

        public NewStrategy()
        {
            _dataGenerator = new DataGenerator();
        }

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<CandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService)
        {
            var candleAndIndicators = candlesLookup[Timeframe.H4];
            if (candleAndIndicators.Count < 20) return null;

            var c = candleAndIndicators[candleAndIndicators.Count - 1];
            var entryPrice = c[Indicator.EMA8].Value + c[Indicator.ATR].Value;
            var stop = entryPrice - c[Indicator.ATR].Value;
            var limit = entryPrice + c[Indicator.ATR].Value;
            var expiry = c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 2);
            var risk = 0.0005M;

            var trade1 = CreateOrder(market.Name, expiry, (decimal)entryPrice, TradeDirection.Long, (decimal?)limit, (decimal)stop, risk);

            var trade2 = CreateMarketOrder(market.Name, TradeDirection.Long, c.Candle, (decimal)stop, risk, (decimal)limit);

            var path = @"model.h5";
            var model = BaseModel.LoadModel(path);
            var inputsCount = 8;
            _dataGenerator.CreateRawData(candlesLookup[Timeframe.D1], candlesLookup[Timeframe.D1].Count - 1, ModelDataType.EMA8 | ModelDataType.EMA25 | ModelDataType.EMA50, inputsCount, out var rawData);
            var x = np.array(np.array(rawData)).reshape(new Shape(1, DataGenerator.GetDataPointsCount(ModelDataType.EMA8 | ModelDataType.EMA25 | ModelDataType.EMA50) * inputsCount));
            var y = model.Predict(x)[0];

            // Get which index is highest
            var maxIndex = 0;
            for (var i = 1; i < y.size; i++)
            {
                if ((float)y[i] > (float)y[maxIndex]) maxIndex = i;
            }


            return new List<Trade> { trade1, trade2 };
        }
    }
}