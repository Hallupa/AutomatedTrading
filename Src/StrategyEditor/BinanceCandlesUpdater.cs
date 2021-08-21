using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hallupa.Library;
using Hallupa.TraderTools.Brokers.Binance;
using log4net;
using TraderTools.Basics;

namespace CryptoTrader
{
    public class BinanceCandlesUpdater
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly BinanceBroker _binance;
        private readonly IBrokersCandlesService _candlesService;
        private readonly IMarketDetailsService _marketDetailsService;

        public BinanceCandlesUpdater(BinanceBroker binance, IBrokersCandlesService candlesService, IMarketDetailsService marketDetailsService)
        {
            _binance = binance;
            _candlesService = candlesService;
            _marketDetailsService = marketDetailsService;
        }

        public Task RunAsync()
        {
            return Task.Run(() =>
            {
                var symbols = _binance.GetSymbols();

                foreach (var s in symbols)
                {
                    _marketDetailsService.AddMarketDetails(new MarketDetails
                    {
                        Broker = "Binance",
                        Name = s
                    });
                }

                _marketDetailsService.SaveMarketDetailsList();

                var added = 0;
                var done = 0;
                var producerConsumer = new ProducerConsumer<(string Symbol, Timeframe Timeframe)>(
                    3,
                    x =>
                    {
                        Log.Info($"Updating candles for {x.Data.Symbol} {x.Data.Timeframe}");
                        _candlesService.UpdateCandles(_binance, x.Data.Symbol, x.Data.Timeframe);
                        Interlocked.Increment(ref done);
                        Log.Info($"Completed candles for {x.Data.Symbol} {x.Data.Timeframe} - total: {((double)done * 100.0) / ((double)added):0.00}%");
                        return ProducerConsumerActionResult.Success;
                    });

                foreach (var symbol in symbols)
                {
                    producerConsumer.Add((symbol, Timeframe.H1));
                    producerConsumer.Add((symbol, Timeframe.H2));
                    producerConsumer.Add((symbol, Timeframe.H4));
                    producerConsumer.Add((symbol, Timeframe.H8));
                    producerConsumer.Add((symbol, Timeframe.D1));

                    if (symbol.EndsWith("BNB") || symbol.EndsWith("USDT") || symbol.EndsWith("BTC"))
                    {
                        producerConsumer.Add((symbol, Timeframe.M5));
                        producerConsumer.Add((symbol, Timeframe.M15));
                        producerConsumer.Add((symbol, Timeframe.M30));
                    }
                }

                added = producerConsumer.QueueLength;
                producerConsumer.SetProducerCompleted();
                producerConsumer.Start();

                producerConsumer.WaitUntilConsumersFinished();

                // Finish window setup
                Log.Info("Symbols updated");
            });
        }
    }
}