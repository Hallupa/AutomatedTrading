using System.Collections.Generic;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.Trading;

namespace AutomatedTraderDesigner
{
    public class SampleStrategy2 : IStrategy
    {
        private const double StopAtrRatio = 0.25;
        private const double LimitRMultiple = 3.0;
        private const double EntryOffsetAtrRatio = 0.1;
        private const double MinTriggerCandleMinAtrRatio = 1.0;
        private const double MaxTriggerCandleMinAtrRatio = 1.5;
        private const int MinTrendingCandles = 5;
        private const Timeframe TargetTimeframe = Timeframe.H2;
        private const double MinEMATrendATRRatio = 0.0;

        public string Name => "Sample";

        public Timeframe[] CandleTimeframesRequired => new[] { Timeframe.H4, Timeframe.H2, Timeframe.D1 };

        public TimeframeLookup<Indicator[]> CreateTimeframeIndicators()
        {
            return new TimeframeLookup<Indicator[]>
            {
                [Timeframe.H2] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR },
                [Timeframe.H4] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR },
                [Timeframe.D1] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR }
            };
        }

        public List<TradeDetails> CreateNewTrades(Timeframe timeframe, MarketDetails market,
            TimeframeLookup<List<BasicCandleAndIndicators>> candlesLookup, List<TradeDetails> existingTrades, ITradeDetailsAutoCalculatorService calculator)
        {
            if (timeframe != TargetTimeframe) return null;

            var candles = candlesLookup[TargetTimeframe];
            if (candles.Count < 20) return null;
            var candle = candles[candles.Count - 1];

            if (!candle[Indicator.EMA8].IsFormed || !candle[Indicator.EMA25].IsFormed || !candle[Indicator.EMA50].IsFormed) return null;
            // var existingTrade = existingTrades.FirstOrDefault(x => x.Market == market.Name && x.CloseDateTime == null);

            // Limit to one trade per day
            if (candle.IsComplete == 0) return null;
            //if (existingTrade != null) return null;

            var trend = GetTrend(candles);
            if (trend == Trend.None) return null;

            if (!IsBigCandleInTrendDirection(candles, trend)) return null;

            if (trend == Trend.Down)
            {
                var entryPrice = candle[Indicator.EMA8].Value + candle[Indicator.ATR].Value * EntryOffsetAtrRatio;
                var stop = candle[Indicator.EMA8].Value + candle[Indicator.ATR].Value * StopAtrRatio;
                var limit = entryPrice - (stop - entryPrice) * LimitRMultiple;
                var trade = TradeDetails.CreateOrder("FXCM", (decimal)entryPrice, candle.CloseTime(),
                    OrderKind.EntryPrice, TradeDirection.Short,
                    1, market.Name, candle.CloseTime().AddSeconds((int)TargetTimeframe * 4), (decimal)stop, (decimal)limit, Timeframe.D1,
                    "8EmaBounce", null, 0, 0, 0, 0, false, OrderType.LimitEntry, calculator);
                return new List<TradeDetails> { trade };
            }
            else
            {
                var entryPrice = candle[Indicator.EMA8].Value - candle[Indicator.ATR].Value * EntryOffsetAtrRatio;
                var stop = candle[Indicator.EMA8].Value - candle[Indicator.ATR].Value * StopAtrRatio;
                var limit = entryPrice + (entryPrice - stop) * LimitRMultiple;
                var trade = TradeDetails.CreateOrder("FXCM", (decimal)entryPrice, candle.CloseTime(),
                    OrderKind.EntryPrice, TradeDirection.Long,
                    1, market.Name, candle.CloseTime().AddSeconds((int)TargetTimeframe * 4), (decimal)stop, (decimal)limit, Timeframe.D1,
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
                var atr = c[Indicator.ATR].Value * MinEMATrendATRRatio;

                if (c[Indicator.EMA8].Value + atr < c[Indicator.EMA25].Value && c[Indicator.EMA25].Value + atr < c[Indicator.EMA50].Value)
                {
                    if (ret == Trend.None || ret == Trend.Down)
                        ret = Trend.Down;
                    else
                        return Trend.None;
                }
                else if (c[Indicator.EMA8].Value - atr > c[Indicator.EMA25].Value && c[Indicator.EMA25].Value - atr > c[Indicator.EMA50].Value)
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