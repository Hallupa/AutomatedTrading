using System;
using System.Collections.Generic;
using TraderTools.Basics;
using TraderTools.Core.Trading;
using TraderTools.Strategy;

namespace AutomatedTrader
{
    public class SampleStrategy : IStrategy
    {
        public string Name => "Sample";

        public Timeframe[] CandleTimeframesRequired => new[] { Timeframe.H4, Timeframe.D1 };

        public TimeframeLookup<Indicator[]> CreateTimeframeIndicators()
        {
            return new TimeframeLookup<Indicator[]>
            {
                [Timeframe.H4] = new[] {Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR},
                [Timeframe.D1] = new[] {Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR}
            };
        }

        public List<TradeDetails> CreateNewTrades(Timeframe timeframe, MarketDetails market, TimeframeLookup<List<BasicCandleAndIndicators>> candlesLookup, List<TradeDetails> existingTrades)
        {
            throw new NotImplementedException();
        }

        public void UpdateExistingOpenTrades(TradeDetails trade, string market, TimeframeLookup<List<BasicCandleAndIndicators>> candles)
        {
            StopHelper.Trail00or50LevelList(trade, market, candles);
        }
    }
}