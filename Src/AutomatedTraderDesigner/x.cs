using System;
using System.Collections.Generic;
using System.Linq;
using Keras.Models;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.Trading;
using TraderTools.Simulation;
using TraderTools.AI;
//using Keras.Models;
//using Numpy;
//using Numpy.Models;
using Numpy;
using Numpy.Models;
using Python.Runtime;

namespace AutomatedTraderDesigner
{
    [RequiredTimeframeCandles(Timeframe.D1, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    public class NewStrategy2 : StrategyBase
    {
        private DataGenerator _dataGenerator;
        public override string Name => "12345";
        private Random _rnd = new Random();
        private BaseModel _model;
        private int _inputsCount;
        private Py.GILState _y;

        public NewStrategy2()
        {
            _dataGenerator = new DataGenerator();
            _inputsCount = 8;
        }

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<CandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService)
        {

            if (_y == null)
            {
                _y = Py.GIL();
                var path = @"C:\Users\Oliver Wickenden\Documents\TraderTools\AutomatedTraderAI\Models\Trend2\model.h5";
                _model = BaseModel.LoadModel(path);
            }

            var candles = candlesLookup[Timeframe.D1];
            if (candles.Count < 20) return null;

            var c = candles[candles.Count - 1];
            var atr = c[Indicator.ATR].Value;

            var rndNumber = _rnd.Next(1, 100);

            if ((rndNumber >= 20 && rndNumber <= 30) || (rndNumber >= 70 && rndNumber <= 80))
            {
                _dataGenerator.CreateRawData(candlesLookup[Timeframe.D1], candlesLookup[Timeframe.D1].Count - 1, ModelDataType.EMAsOnly, _inputsCount, out var rawData);

                //using (Py.GIL())
                {
                    //var x1 = np.array(np.array(rawData));
                    //var x = x1.reshape(new Shape(1, DataGenerator.GetDataPointsCount(ModelDataType.EMAsOnly) * _inputsCount));
                    //var y = _model.Predict(x)[0];

                    var x = np.array(np.array(rawData)).reshape(new Shape(1, DataGenerator.GetDataPointsCount(ModelDataType.EMAsOnly) * _inputsCount));

                    var y = _model.Predict(x)[0];

                    // Get which index is highest
                    var maxIndex = 0;
                    for (var i = 1; i < y.size; i++)
                    {
                        if ((float)y[i] > (float)y[maxIndex]) maxIndex = i;
                    }
    
                    if (maxIndex == 3)
                    {
                        var entryPrice = c[Indicator.EMA8].Value; //c.Candle.CloseAsk;
                        var stop = entryPrice - atr;
                        var limit = entryPrice + atr;
                        // var t = CreateMarketOrder(market.Name, TradeDirection.Long, c.Candle, (decimal)stop, 0.005M, (decimal)limit);
                        var t = CreateOrder(market.Name, c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 2),
                            (decimal)entryPrice, TradeDirection.Long, (decimal)c.Candle.CloseAsk,
                            c.Candle.CloseTime(), (decimal?)limit, (decimal)stop, 0.005M);


                        return new List<Trade> { t };
                    }
    
                    if (maxIndex == 1)
                    {
                       /* var entryPrice = c.Candle.CloseBid;
                        var stop = entryPrice + atr;
                        var limit = entryPrice - atr;
                        var t = CreateMarketOrder(market.Name, TradeDirection.Short, c.Candle, (decimal)stop, 0.005M, (decimal)limit);
                        return new List<Trade> { t };*/
                    }
                }
            }

            /*if (rndNumber >= 20 && rndNumber <= 30)
            {
            	var entryPrice = c.Candle.CloseAsk;
            	var stop = entryPrice - atr;
            	var limit = entryPrice + atr;
        		var t = CreateMarketOrder(market.Name, TradeDirection.Long, c.Candle, (decimal)stop, 0.005M, (decimal)limit);
        		t.Custom1 = _rnd.Next(1, 10000000);
            	return new List<Trade> { t };
            }
            
            if (rndNumber >= 70 && rndNumber <= 80)
            {
            	var entryPrice = c.Candle.CloseBid;
            	var stop = entryPrice + atr;
            	var limit = entryPrice - atr;
        		var t = CreateMarketOrder(market.Name, TradeDirection.Short, c.Candle, (decimal)stop, 0.005M, (decimal)limit);
        		t.Custom1 = _rnd.Next(1, 10000000);
            	return new List<Trade> { t };
            }*/

            return null;
        }
    }
}