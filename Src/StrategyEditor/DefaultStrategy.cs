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
        private Dictionary<string, IndicatorValues> _atr = new Dictionary<string, IndicatorValues>();

        public DefaultStrategy()
        {
            _rnd = new Random();
            SetTimeframes(Timeframe.H2);

            AddMajors();
            AddMinors();
            AddMajorIndices();

            foreach (var m in Markets)
            {
                _atr[m] = ATR(m, Timeframe.H2);
            }
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            foreach (var added in addedCandleTimeframes)
            {
                if (!_atr[added.Market].HasValue) continue;


                var c = Candles[added.Market][Timeframe.H2].Last();
                var r = _rnd.Next(0, 20);
                var atr = _atr[added.Market];

                if (r == 5)
                {
                    MarketShort(added.Market, Balance / (decimal)c.CloseBid, (decimal)(c.CloseBid + atr.Value), (decimal)(c.CloseBid - atr.Value));
                }
                else if (r == 10)
                {
                    MarketLong(added.Market, Balance / (decimal)c.CloseAsk, (decimal)(c.CloseAsk - atr.Value), (decimal)(c.CloseAsk + atr.Value));
                }
            }
        }
    }
}