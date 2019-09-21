using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
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
        public override string Name => "Name";

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<CandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService)
        {
            var andIndicators = candlesLookup[Timeframe.H4];
            if (andIndicators.Count < 20) return null;

            var c = andIndicators[andIndicators.Count - 1];
            var entryPrice = c[Indicator.EMA8].Value + c[Indicator.ATR].Value;
            var stop = entryPrice - c[Indicator.ATR].Value;
            var limit = entryPrice + c[Indicator.ATR].Value;
            var expiry = c.Candle.CloseTime().AddSeconds((int)Timeframe.H4 * 2);
            var risk = 0.0005M;

            var trade1 = CreateOrder(market.Name, expiry,
                (decimal)entryPrice, TradeDirection.Long, (decimal)andIndicators[andIndicators.Count - 1].Candle.CloseBid,
                andIndicators[andIndicators.Count - 1].Candle.CloseTime(), (decimal?)limit, (decimal)stop, risk);

            var trade2 = CreateMarketOrder(market.Name, TradeDirection.Long, c.Candle, (decimal)stop, risk, (decimal)limit);


            return new List<Trade> { trade1, trade2 };
        }
    }
}