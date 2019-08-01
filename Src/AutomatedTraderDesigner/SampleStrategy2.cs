using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Basics.Helpers;
using TraderTools.Core.Extensions;
using TraderTools.Core.Trading;

namespace AutomatedTraderDesigner
{
    //	Stop ATR	Limit	Stop ATR	Limit	
    //	0.25	1.1	826		0.5	1.1	
    //	0.25	1.2	841		0.5	1.2	
    //	0.25	1.3	840		0.5	1.3	
    //	0.25	1.4	859		0.5	1.4	    805
    //	0.25	1.5	845		0.5	1.5	    813
    //	0.25	2	741		0.5	2	    770
    //	0.25	3	544		0.5	3	    520
    //	0.25	4	574		0.5	4	
    //	0.25	5	535		0.5	5	
    //	0.25	6	455		0.5	6	
    //	0.25	7	373		0.5	7	
    //	0.25	8	319		0.5	8	


    public class SampleStrategy2 : StrategyBase
    {
        private const double StopAtrRatio = 0.5;
        private const double LimitRMultiple = 2.5;
        private const double EntryOffsetAtrRatio = 0.1;
        private const double MinTriggerCandleMinAtrRatio = 1.1;
        private const double MaxTriggerCandleMinAtrRatio = 10.0;
        private const int MinTrendingCandles = 5;
        private const Timeframe TargetTimeframe = Timeframe.H2;
        private const double MinEMATrendATRRatio = 0.0;

        public override string Name => "Trend Strategy 2";

        public override Timeframe[] CandleTimeframesRequired => new[] { Timeframe.H4, Timeframe.H2, Timeframe.D1 };

        public override TimeframeLookup<Indicator[]> CreateTimeframeIndicators()
        {
            return new TimeframeLookup<Indicator[]>
            {
                [Timeframe.H2] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR },
                [Timeframe.H4] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR },
                [Timeframe.D1] = new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR }
            };
        }

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<BasicCandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService)
        {
            var candles = candlesLookup[TargetTimeframe];
            if (candles.Count < 20) return null;
            if (existingTrades != null && existingTrades.Count(t => t.CloseDateTime == null) > 0) return null;

            for (var candleOffset = 0; candleOffset <= 1; candleOffset++)
            {
                if (candleOffset > 0 && candles[candles.Count - 1 - candleOffset + 1].IsComplete == 1) return null;

                var candle = candles[candles.Count - 1 - candleOffset];

                if (!candle[Indicator.EMA8].IsFormed || !candle[Indicator.EMA25].IsFormed ||
                    !candle[Indicator.EMA50].IsFormed || candle.IsComplete != 1) continue;

                if (candleOffset > 0 && candle.CloseTimeTicks < DateTime.UtcNow.Ticks - TimeSpan.FromMinutes(20).Ticks) continue;

                var trend = GetTrend(candles, candleOffset);
                if (trend == Trend.None) continue;

                if (!IsBigCandleInTrendDirection(candles, trend, candleOffset)) continue;

                if (trend == Trend.Down)
                {
                    var entryPrice = candle[Indicator.EMA8].Value + candle[Indicator.ATR].Value * EntryOffsetAtrRatio;
                    var stop = candle[Indicator.EMA8].Value + candle[Indicator.ATR].Value * StopAtrRatio;
                    var limit = entryPrice - (stop - entryPrice) * LimitRMultiple;

                    var trade = CreateOrder(market.Name, candle.CloseTime().AddSeconds((int)TargetTimeframe * 4),
                        (decimal)entryPrice, TradeDirection.Short, (decimal)candles[candles.Count - 1].Close,
                        candles[candles.Count - 1].CloseTime(), (decimal)limit, (decimal)stop, 0.0005M);
                    trade.Strategies = "TrendStrategy2";

                    return new List<Trade> { trade };
                }
                else
                {
                    var entryPrice = candle[Indicator.EMA8].Value - candle[Indicator.ATR].Value * EntryOffsetAtrRatio;
                    var stop = candle[Indicator.EMA8].Value - candle[Indicator.ATR].Value * StopAtrRatio;
                    var limit = entryPrice + (entryPrice - stop) * LimitRMultiple;

                    var trade = CreateOrder(market.Name, candle.CloseTime().AddSeconds((int)TargetTimeframe * 4),
                        (decimal)entryPrice, TradeDirection.Long, (decimal)candles[candles.Count - 1].Close,
                        candles[candles.Count - 1].CloseTime(), (decimal)limit, (decimal)stop, 0.0005M);
                    trade.Strategies = "TrendStrategy2";

                    return new List<Trade> { trade };
                }
            }

            return null;
        }

        private enum Trend
        {
            Up,
            Down,
            None
        }

        private bool IsBigCandleInTrendDirection(List<BasicCandleAndIndicators> candles, Trend trend, int candleOffset)
        {
            var c = candles[candles.Count - 1 - candleOffset];

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

        private Trend GetTrend(List<BasicCandleAndIndicators> candles, int candleOffset)
        {
            if (candles.Count < 20) return Trend.None;

            // Last candles must be trending
            var ret = Trend.None;
            for (var i = candles.Count - MinTrendingCandles - candleOffset; i < candles.Count - candleOffset; i++)
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