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

        private Subject<List<TradeDetails>> _testRunCompleted = new Subject<List<TradeDetails>>();

        private Subject<List<TradeDetails>> _testRunStarted = new Subject<List<TradeDetails>>();

        public IObservable<List<TradeDetails>> TestRunCompleted => _testRunCompleted.AsObservable();

        public IObservable<List<TradeDetails>> TestRunStarted => _testRunStarted.AsObservable();

        public void AddResult(List<TradeDetails> result)
        {
            lock (Results)
            {
                Results.AddRange(result);
            }

            _testResultsUpdated.OnNext(result);
        }

        public void RaiseTestRunCompleted()
        {
            _testRunCompleted.OnNext(Results);
        }

        public void RaiseTestRunStarted()
        {
            _testRunStarted.OnNext(Results);
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