using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using log4net;
using TraderTools.Core.Services;
using TraderTools.Core.UI.ViewModels;

namespace AutomatedTraderDesigner.ViewModels
{
    public class StrategyCustomRunViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string _savedCodePath;
        private string _codeText;
        [Import] private StrategyRunnerResultsService _results;
        private IDisposable _testResultsUpdatedObserver;

        public StrategyCustomRunViewModel()
        {
            DependencyContainer.ComposeParts(this);
            SaveCommand = new DelegateCommand(o => Save());
            _savedCodePath = Path.Combine(BrokersService.DataDirectory, "StrategyTester", "SavedCode.txt");
            if (File.Exists(_savedCodePath))
            {
                CodeText = File.ReadAllText(_savedCodePath);
            }

            ResultsViewModel = new TradesResultsViewModel(() =>
                {
                    lock (_results.Results)
                    {
                        return _results.Results.ToList();
                    }
                });

            _testResultsUpdatedObserver = _results.TestRunCompleted.Subscribe(newResults =>
            {
                ResultsViewModel.UpdateResults();
            });

            _testResultsUpdatedObserver = _results.TestRunStarted.Subscribe(newResults =>
            {
                ResultsViewModel.UpdateResults();
            });

            Task.Run(() =>
            {
                Log.Info("Updating strategy run results");
                ResultsViewModel.UpdateResults();
                Log.Info("Updated strategy run results");
            });
        }

        public TradesResultsViewModel ResultsViewModel { get; }

        private void Save()
        {
            File.WriteAllText(_savedCodePath, CodeText);
        }

        public DelegateCommand SaveCommand { get; private set; }

        public string CodeText
        {
            get => _codeText;
            set
            {
                _codeText = value;
                StrategyRunViewModel.CustomCode = _codeText;
            }
        }
    }
}