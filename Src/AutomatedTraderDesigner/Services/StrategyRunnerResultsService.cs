using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using TraderTools.Basics;

namespace AutomatedTraderDesigner.Services
{
    [Export(typeof(StrategyRunnerResultsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class StrategyRunnerResultsService
    {
        private Subject<List<TradeDetails>> _testResultsUpdated = new Subject<List<TradeDetails>>();

        public IObservable<List<TradeDetails>> TestResultsUpdated => _testResultsUpdated.AsObservable();

        public void AddResult(List<TradeDetails> result)
        {
            lock (Results)
            {
                Results.AddRange(result);
            }

            _testResultsUpdated.OnNext(result);
        }

        public void Reset()
        {
            lock (Results)
            {
                Results.Clear();
            }

            _testResultsUpdated.OnNext(new List<TradeDetails>());
        }

        public List<TradeDetails> Results { get; } = new List<TradeDetails>();
    }
}