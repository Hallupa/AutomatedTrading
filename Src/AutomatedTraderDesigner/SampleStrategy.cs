using System.Collections.Generic;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.Trading;

namespace AutomatedTrader
{
    public class SampleStrategy : IStrategy
    {
        private const double StopAtrRatio = 0.10;
        private const double LimitRMultiple = 3.0;
        private const double MinTriggerCandleMinAtrRatio = 1.0;
        private const double MaxTriggerCandleMinAtrRatio = 1.5;
        private const int MinTrendingCandles = 5;

        public string Name => "Sample";

        public Timeframe[] CandleTimeframesRequired => new[] { Timeframe.H4, Timeframe.H2, Timeframe.D1 };

        public TimeframeLookup<Indicator[]> CreateTimeframeIndicators()
        {
            return new TimeframeLookup<Indicator[]>
            {
                [Timeframe.H4] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR },
                [Timeframe.D1] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR }
            };
        }

        public List<TradeDetails> CreateNewTrades(Timeframe timeframe, MarketDetails market,
            TimeframeLookup<List<BasicCandleAndIndicators>> candlesLookup, List<TradeDetails> existingTrades, ITradeDetailsAutoCalculatorService calculator)
        {
            if (timeframe != Timeframe.D1) return null;

            var dayCandles = candlesLookup[Timeframe.D1];
            if (dayCandles.Count < 20) return null;
            var dayCandle = dayCandles[dayCandles.Count - 1];

            if (!dayCandle[Indicator.EMA8].IsFormed || !dayCandle[Indicator.EMA25].IsFormed || !dayCandle[Indicator.EMA50].IsFormed) return null;
            // var existingTrade = existingTrades.FirstOrDefault(x => x.Market == market.Name && x.CloseDateTime == null);

            // Limit to one trade per day
            if (dayCandle.IsComplete == 0) return null;
            //if (existingTrade != null) return null;

            var trend = GetTrend(dayCandles);
            if (trend == Trend.None) return null;

            if (!IsBigCandleInTrendDirection(dayCandles, trend)) return null;

            if (trend == Trend.Down)
            {
                var entryPrice = dayCandle[Indicator.EMA8].Value;
                var stop = dayCandle[Indicator.EMA8].Value + dayCandle[Indicator.ATR].Value * StopAtrRatio;
                var limit = entryPrice - (stop - entryPrice) * LimitRMultiple;
                var trade = TradeDetails.CreateOrder("FXCM", (decimal)entryPrice, dayCandle.CloseTime(),
                    OrderKind.EntryPrice, TradeDirection.Short,
                    1, market.Name, dayCandle.CloseTime().AddDays(4), (decimal)stop, (decimal)limit, Timeframe.D1,
                    "8EmaBounce", null, 0, 0, 0, 0, false, OrderType.LimitEntry, calculator);
                return new List<TradeDetails> { trade };
            }
            else
            {
                var entryPrice = dayCandle[Indicator.EMA8].Value;
                var stop = dayCandle[Indicator.EMA8].Value - dayCandle[Indicator.ATR].Value * StopAtrRatio;
                var limit = entryPrice + (entryPrice - stop) * LimitRMultiple;
                var trade = TradeDetails.CreateOrder("FXCM", (decimal)entryPrice, dayCandle.CloseTime(),
                    OrderKind.EntryPrice, TradeDirection.Long,
                    1, market.Name, dayCandle.CloseTime().AddDays(4), (decimal)stop, (decimal)limit, Timeframe.D1,
                    "8EmaBounce", null, 0, 0, 0, 0, false, OrderType.LimitEntry, calculator);
                return new List<TradeDetails> { trade };
            }
        }

        public void UpdateExistingOpenTrades(TradeDetails trade, string market, TimeframeLookup<List<BasicCandleAndIndicators>> candles)
        {
            //StopHelper.Trail00or50LevelList(trade, market, candles);
        }

        private enum Trend
        {
            Up,
            Down,
            None
        }

        private bool IsBigCandleInTrendDirection(List<BasicCandleAndIndicators> candles, Trend trend)
        {
            var c = candles[candles.Count - 1];

            if (trend == Trend.Down)
            {
                var minPoint = c[Indicator.EMA8].Value - c[Indicator.ATR].Value * MinTriggerCandleMinAtrRatio;
                var maxPoint = c[Indicator.EMA8].Value - c[Indicator.ATR].Value * MaxTriggerCandleMinAtrRatio;
                if (c.Close < minPoint && c.Close > maxPoint && c.Colour() == CandleColour.Black) return true;
            }

            if (trend == Trend.Up)
            {
                var minPoint = c[Indicator.EMA8].Value + c[Indicator.ATR].Value * MinTriggerCandleMinAtrRatio;
                var maxPoint = c[Indicator.EMA8].Value + c[Indicator.ATR].Value * MaxTriggerCandleMinAtrRatio;
                if (c.Close > minPoint && c.Close < maxPoint && c.Colour() == CandleColour.White) return true;
            }

            return false;
        }

        private Trend GetTrend(List<BasicCandleAndIndicators> candles)
        {
            if (candles.Count < 20) return Trend.None;

            // Last candles must be trending
            var ret = Trend.None;
            for (var i = candles.Count - MinTrendingCandles; i < candles.Count; i++)
            {
                var c = candles[i];

                if (c[Indicator.EMA8].Value < c[Indicator.EMA25].Value && c[Indicator.EMA25].Value < c[Indicator.EMA50].Value)
                {
                    if (ret == Trend.None || ret == Trend.Down)
                        ret = Trend.Down;
                    else
                        return Trend.None;
                }
                else if (c[Indicator.EMA8].Value > c[Indicator.EMA25].Value && c[Indicator.EMA25].Value > c[Indicator.EMA50].Value)
                {
                    if (ret == Trend.None || ret == Trend.Up)
                        ret = Trend.Up;
                    else
                        return Trend.None;
                }
                else
                {
                    return Trend.None;
                }
            }

            return ret;
        }
    }
}