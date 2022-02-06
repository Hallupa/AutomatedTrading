using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Hallupa.TraderTools.Basics;
using Hallupa.TraderTools.Simulation;
using TraderTools.Basics;

namespace StrategyEditor.Services
{
    [Export(typeof(StrategyRunnerResultsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class StrategyRunnerResultsService
    {
        private StrategyBase _strategyBase;
        private Dictionary<string, AssetBalance> _initialAssetBalances;
        private Subject<(List<Trade> Trades, StrategyBase Strategy)> _testResultsUpdated = new Subject<(List<Trade> Trades, StrategyBase Strategy)>();

        public IObservable<(List<Trade> Trades, StrategyBase Strategy)> TestResultsUpdated => _testResultsUpdated.AsObservable();

        private Subject<(List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances)> _testRunCompleted = new Subject<(List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances)>();

        private Subject<(List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances)> _testRunStarted = new Subject<(List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances)>();

        public IObservable<(List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances)> TestRunCompleted => 
            _testRunCompleted.AsObservable();

        public IObservable<(List<Trade> Trades, StrategyBase Strategy, Dictionary<string, AssetBalance> InitialAssetBalances)> TestRunStarted => _testRunStarted.AsObservable();

        public void AddResult(List<Trade> result, StrategyBase strategyBase, Dictionary<string, AssetBalance> initialAssetBalances)
        {
            lock (Results)
            {
                Results.AddRange(result);
            }

            _strategyBase = strategyBase;
            _initialAssetBalances = initialAssetBalances;
            _testResultsUpdated.OnNext((result, _strategyBase));
        }

        public void RaiseTestRunCompleted()
        {
            _testRunCompleted.OnNext((Results, _strategyBase, _initialAssetBalances));
        }

        public void RaiseTestRunStarted()
        {
            _testRunStarted.OnNext((Results, _strategyBase, _initialAssetBalances));
        }

        public void Reset()
        {
            lock (Results)
            {
                Results.Clear();
            }

            _testResultsUpdated.OnNext((new List<Trade>(), null));
        }

        public List<Trade> Results { get; } = new List<Trade>();
    }
}