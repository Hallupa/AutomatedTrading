using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Hallupa.Library;
using TraderTools.Basics;
using TraderTools.Core.Services;

namespace AutomatedTrader.ViewModels
{
    public class TradesViewModel
    {
        [Import]
        private BrokersService _brokerService;

        public TradesViewModel()
        {
            DependencyContainer.ComposeParts(this);

            /*foreach (var trade in _brokerService.BrokerTrades.Trades.OrderByDescending(x => x.Time))
            {
                Trades.Add(trade);
            }*/
        }

        public ObservableCollection<TradeDetails> Trades { get; } = new ObservableCollection<TradeDetails>();
    }
}