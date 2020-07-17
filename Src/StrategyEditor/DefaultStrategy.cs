using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace StrategyEditor
{
    public class DefaultStrategy : StrategyBase
    {
        private Random _rnd;
        private IndicatorValues _atr;

        public DefaultStrategy()
        {
            _rnd = new Random();
            _atr = ATR(Timeframe.H2);
            SetTimeframes(Timeframe.H2);

            AddMajors();
            AddMinors();
            AddMajorIndices();

            SetRiskEquityPercent(0.2M);
        }

        public override void ProcessCandles(List<Timeframe> newCandleTimeframes)
        {
            if (!_atr.HasValue) return;

            var c = Candles[Timeframe.H2].Last();
            var r = _rnd.Next(0, 20);

            if (r == 5)
            {
                MarketShort((decimal)(c.CloseBid + _atr.Value), (decimal)(c.CloseBid - _atr.Value));
            }
            else if (r == 10)
            {
                MarketLong((decimal)(c.CloseAsk - _atr.Value), (decimal)(c.CloseAsk + _atr.Value));
            }
        }
    }
}