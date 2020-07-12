using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using TraderTools.Basics;

namespace StrategyEditor.Services
{
    [Export(typeof(StrategyRunnerResultsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class StrategyRunnerResultsService
    {
        private Subject<List<Trade>> _testResultsUpdated = new Subject<List<Trade>>();

        public IObservable<List<Trade>> TestResultsUpdated => _testResultsUpdated.AsObservable();

        private Subject<List<Trade>> _testRunCompleted = new Subject<List<Trade>>();

        private Subject<List<Trade>> _testRunStarted = new Subject<List<Trade>>();

        public IObservable<List<Trade>> TestRunCompleted => _testRunCompleted.AsObservable();

        public IObservable<List<Trade>> TestRunStarted => _testRunStarted.AsObservable();

        public void AddResult(List<Trade> result)
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

            _testResultsUpdated.OnNext(new List<Trade>());
        }

        public List<Trade> Results { get; } = new List<Trade>();
    }
}