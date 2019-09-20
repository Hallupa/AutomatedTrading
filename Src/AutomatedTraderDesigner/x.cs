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
    [StopTrailIndicator(Timeframe.D1, Indicator.EMA8)]
    [RequiredTimeframeCandles(Timeframe.H2, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    [RequiredTimeframeCandles(Timeframe.D1, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    [RequiredTimeframeCandles(Timeframe.H4, Indicator.EMA8, Indicator.EMA25, Indicator.EMA50, Indicator.ATR)]
    public class TrendStrategy : StrategyBase
    {
        private const double StopAtrRatio = 5.5, EntryOffsetAtrRatio = -1, MinEMATrendATRRatio = 0.35, MinTriggerCandleMinAtrRatio = 1.5, MaxTriggerCandleMinAtrRatio = 5, SpreadMaxRatioOfATR = 0.2;
        private const int MinTrendingCandles = 3, OrderExpiryCandles = 5, FanningCandles = 4;
        private decimal? LimitRMultiple = null;//3M;

        public override string Name => "TestC - trend with EMAs compressing";

        public override List<Trade> CreateNewTrades(
            MarketDetails market, TimeframeLookup<List<CandleAndIndicators>> candlesLookup,
            List<Trade> existingTrades, ITradeDetailsAutoCalculatorService calculatorService)
        {
            List<CandleAndIndicators> candles = candlesLookup[Timeframe.H4], d1Candles = candlesLookup[Timeframe.D1];
            if (candles.Count < 20 || d1Candles.Count < 20) return null;
            var candle = candles[candles.Count - 1];
            if (!candle[Indicator.EMA8].IsFormed || !candle[Indicator.EMA25].IsFormed || !candle[Indicator.EMA50].IsFormed || candle.Candle.IsComplete != 1) return null;

            var trend = GetEMAsTrendAndFanning(candles);
            if (trend == Trend.None) return null;

            if (trend != GetEMAsTrendAndFanning(d1Candles)) return null;

            /*var spread = Math.Abs(candle.Candle.CloseBid - candle.Candle.CloseAsk);
            var atr = candle[Indicator.ATR].Value;
            if (spread > atr * SpreadMaxRatioOfATR) return null;

            var trend = GetTrend(candles, 0);
            if (trend == Trend.None) return null;

            var d1Trend = GetTrend(d1Candles, 0);
            if (d1Trend != trend) return null;

            for (var i = 0; i < 5; i++)
            {
                // Check EMAs are lined up
                if (!IsEMAsLinedUp(d1Candles, d1Trend, i)) return null;
                if (!IsEMAsLinedUp(candles, d1Trend, i)) return null;
            }*/


            //if (!IsBigCandleInTrendDirection(candles, trend, 0)) return null;

            return CreateTrade(market, trend, candle, candles);
        }

        private static Trend GetEMAsTrendAndCompressing(List<CandleAndIndicators> candles)
        {
            var trend = Trend.None;
            float? dist = null;
            var minCompressingATRRatio = 0.15;
            var maxFirstCandleATRRatio = 0.2;
            for (var i = FanningCandles - 1; i >= 0; i--)
            {
                if (!(candles[candles.Count - 1 - i][Indicator.EMA50].Value >
                      candles[candles.Count - 1 - i][Indicator.EMA25].Value
                      && candles[candles.Count - 1 - i][Indicator.EMA25].Value >
                      candles[candles.Count - 1 - i][Indicator.EMA8].Value)) break;

                var newDist = Math.Abs(candles[candles.Count - 1 - i][Indicator.EMA50].Value -
                                       candles[candles.Count - 1 - i][Indicator.EMA8].Value);
                var atr = candles[candles.Count - 1 - i][Indicator.ATR].Value;
                if (i == 0 && newDist > atr * maxFirstCandleATRRatio) break;

                if (dist != null && newDist > dist - atr * minCompressingATRRatio) break;
                dist = newDist;

                if (i == 0)
                {
                    trend = Trend.Down;
                }
            }

            if (trend != Trend.Down)
            {
                for (var i = FanningCandles - 1; i >= 0; i--)
                {
                    if (!(candles[candles.Count - 1 - i][Indicator.EMA50].Value <
                          candles[candles.Count - 1 - i][Indicator.EMA25].Value
                          && candles[candles.Count - 1 - i][Indicator.EMA25].Value <
                          candles[candles.Count - 1 - i][Indicator.EMA8].Value)) break;

                    var newDist = Math.Abs(candles[candles.Count - 1 - i][Indicator.EMA50].Value -
                                           candles[candles.Count - 1 - i][Indicator.EMA8].Value);
                    var atr = candles[candles.Count - 1 - i][Indicator.ATR].Value;
                    if (i == 0 && newDist > atr * maxFirstCandleATRRatio) break;

                    if (dist != null && newDist > dist - atr * minCompressingATRRatio) break;
                    dist = newDist;

                    if (i == 0)
                    {
                        trend = Trend.Up;
                    }
                }
            }

            return trend;
        }

        private static Trend GetEMAsTrendAndFanning(List<CandleAndIndicators> candles)
        {
            var trend = Trend.None;
            float? dist = null;
            var minFanningATRRatio = 0.15;
            for (var i = FanningCandles - 1; i >= 0; i--)
            {
                if (!(candles[candles.Count - 1 - i][Indicator.EMA50].Value >
                      candles[candles.Count - 1 - i][Indicator.EMA25].Value
                      && candles[candles.Count - 1 - i][Indicator.EMA25].Value >
                      candles[candles.Count - 1 - i][Indicator.EMA8].Value)) break;

                var newDist = Math.Abs(candles[candles.Count - 1 - i][Indicator.EMA50].Value -
                                       candles[candles.Count - 1 - i][Indicator.EMA8].Value);
                var atr = candles[candles.Count - 1 - i][Indicator.ATR].Value;
                if (newDist < atr * 0.1) break;

                if (dist != null && newDist < dist + atr * minFanningATRRatio) break;
                dist = newDist;

                if (i == 0)
                {
                    trend = Trend.Down;
                }
            }

            if (trend != Trend.Down)
            {
                for (var i = FanningCandles - 1; i >= 0; i--)
                {
                    if (!(candles[candles.Count - 1 - i][Indicator.EMA50].Value <
                          candles[candles.Count - 1 - i][Indicator.EMA25].Value
                          && candles[candles.Count - 1 - i][Indicator.EMA25].Value <
                          candles[candles.Count - 1 - i][Indicator.EMA8].Value)) break;

                    var newDist = Math.Abs(candles[candles.Count - 1 - i][Indicator.EMA50].Value -
                                           candles[candles.Count - 1 - i][Indicator.EMA8].Value);
                    var atr = candles[candles.Count - 1 - i][Indicator.ATR].Value;
                    if (newDist < atr * 0.1) break;

                    if (dist != null && newDist < dist + atr * minFanningATRRatio) break;
                    dist = newDist;

                    if (i == 0)
                    {
                        trend = Trend.Up;
                    }
                }
            }

            return trend;
        }

        private List<Trade> CreateTrade(MarketDetails market, Trend trend, CandleAndIndicators candle, List<CandleAndIndicators> candles)
        {
            if (trend == Trend.Down)
            {
                var entryPrice = candle[Indicator.EMA8].Value + candle[Indicator.ATR].Value * EntryOffsetAtrRatio;
                //var stop = entryPrice + candle[Indicator.ATR].Value * StopAtrRatio;
                var stop = candle[Indicator.EMA25].Value + candle[Indicator.ATR].Value * StopAtrRatio;
                var limit = LimitRMultiple != null
                    ? (decimal)entryPrice - (Math.Abs((decimal)entryPrice - (decimal)stop) * LimitRMultiple.Value)
                    : (decimal?)null;

                var trade = CreateOrder(market.Name,
                    candle.Candle.CloseTime().AddSeconds((int)Timeframe.H2 * OrderExpiryCandles),
                    (decimal)entryPrice, TradeDirection.Short, (decimal)candles[candles.Count - 1].Candle.CloseBid,
                    candles[candles.Count - 1].Candle.CloseTime(), (decimal?)limit, (decimal)stop, 0.0005M);

                return new List<Trade> { trade };
            }
            else
            {
                var entryPrice = candle[Indicator.EMA8].Value - candle[Indicator.ATR].Value * EntryOffsetAtrRatio;
                //var stop = entryPrice - candle[Indicator.ATR].Value * StopAtrRatio;
                var stop = candle[Indicator.EMA25].Value - candle[Indicator.ATR].Value * StopAtrRatio;
                var limit = LimitRMultiple != null
                    ? (decimal)entryPrice + (Math.Abs((decimal)entryPrice - (decimal)stop) * LimitRMultiple.Value)
                    : (decimal?)null;

                var trade = CreateOrder(market.Name,
                    candle.Candle.CloseTime().AddSeconds((int)Timeframe.H2 * OrderExpiryCandles),
                    (decimal)entryPrice, TradeDirection.Long, (decimal)candles[candles.Count - 1].Candle.CloseAsk,
                    candles[candles.Count - 1].Candle.CloseTime(), (decimal?)limit, (decimal)stop, 0.0005M);

                return new List<Trade> { trade };
            }
        }

        private enum Trend
        {
            Up,
            Down,
            None
        }

        private bool IsBigCandleInTrendDirection(List<CandleAndIndicators> candles, Trend trend, int candleOffset)
        {
            var c = candles[candles.Count - 1 - candleOffset];

            if (trend == Trend.Down)
            {
                var minPoint = c[Indicator.EMA8].Value - c[Indicator.ATR].Value * MinTriggerCandleMinAtrRatio;
                var maxPoint = c[Indicator.EMA8].Value - c[Indicator.ATR].Value * MaxTriggerCandleMinAtrRatio;
                if (c.Candle.CloseBid < minPoint && c.Candle.CloseBid > maxPoint && c.Colour() == CandleColour.Black) return true;
            }

            if (trend == Trend.Up)
            {
                var minPoint = c[Indicator.EMA8].Value + c[Indicator.ATR].Value * MinTriggerCandleMinAtrRatio;
                var maxPoint = c[Indicator.EMA8].Value + c[Indicator.ATR].Value * MaxTriggerCandleMinAtrRatio;
                if (c.Candle.CloseAsk > minPoint && c.Candle.CloseAsk < maxPoint && c.Colour() == CandleColour.White) return true;
            }

            return false;
        }

        private bool IsEMAsLinedUp(List<CandleAndIndicators> candles, Trend trend, int candleOffset)
        {
            var c = candles[candles.Count - 1 - candleOffset];

            if (trend == Trend.Down && !(c[Indicator.EMA8].Value < c[Indicator.EMA25].Value && c[Indicator.EMA25].Value < c[Indicator.EMA50].Value)) return false;
            if (trend == Trend.Up && !(c[Indicator.EMA8].Value > c[Indicator.EMA25].Value && c[Indicator.EMA25].Value > c[Indicator.EMA50].Value)) return false;

            return true;
        }

        private Trend GetTrend(List<CandleAndIndicators> candles, int candleOffset)
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