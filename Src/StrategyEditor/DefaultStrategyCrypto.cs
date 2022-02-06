using System.Collections.Generic;
using Hallupa.TraderTools.Basics;
using Hallupa.TraderTools.Simulation;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace StrategyEditor
{
    public class ETHUSDT_EMACross : StrategyBase
    {
        private readonly IndicatorValues _ema8;
        private readonly IndicatorValues _ema21;

        public ETHUSDT_EMACross()
        {
            SetSimulationGranularity(Timeframe.M5);
            SetCommission(0.00075M); // With BNB discount
            SetBroker("Binance");
            SetMarkets("ETHUSDT");
            SetTimeframes(Timeframe.H1);
            SetSimulationInitialBalance(new AssetBalance("USDT", 10000M));

            _ema21 = EMA("ETHUSDT", Timeframe.H1, 21);
            _ema8 = EMA("ETHUSDT", Timeframe.H1, 8);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (!_ema8.IsFormed || !_ema21.IsFormed) return;

            if (_ema8.Value > _ema21.Value)
            {
                var balance = GetCurrentBalance("USDT");
                if (balance <= 0.1M) return;

                MarketBuy("ETHUSDT", "ETH", balance);
            }

            if (_ema8.Value < _ema21.Value)
            {
                var balance = GetCurrentBalance("ETH");
                if (balance <= 0.1M) return;

                MarketSell("ETHUSDT", "ETH", balance);
            }
        }
    }
}