using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AutomatedTraderDesigner.Services;
using Hallupa.Library;
using Hallupa.Library.UI.Views;
using log4net;
using TraderTools.Basics;
using TraderTools.Core.Services;
using TraderTools.Core.UI.ViewModels;

namespace AutomatedTraderDesigner.ViewModels
{
    public class StrategyCustomRunViewModel : INotifyPropertyChanged
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string _strategiesDirectory;
        private string _codeText;
        [Import] private StrategyRunnerResultsService _results;
        [Import] private StrategyService _strategyService;
        [Import] private UIService _uiService;
        [Import] private IDataDirectoryService _dataDirectoryService;

        private IDisposable _testResultsUpdatedObserver;
        private string _selectedStrategyFilename;
        private string _defaultStrategyText;

        public StrategyCustomRunViewModel()
        {
            DependencyContainer.ComposeParts(this);
            SaveCommand = new DelegateCommand(o =>
            {
                Save();
                UpdateStrategiesService();
            });
            CreateStrategyCommand = new DelegateCommand(o => CreateStrategy());
            DeleteStrategyCommand = new DelegateCommand(o => DeleteStrategy());
            _strategiesDirectory = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName);

            if (!Directory.Exists(_strategiesDirectory))
            {
                Directory.CreateDirectory(_strategiesDirectory);
            }

            LoadDefaultStrategyText();
            RefreshStrategyFilenames();
            UpdateStrategiesService();

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

            _uiService.RegisterF5Action(() =>
            {
                Save();
                UpdateStrategiesService();
            }, true);
        }

        private void DeleteStrategy()
        {
            if (string.IsNullOrEmpty(SelectedStrategyFilename))
            {
                return;
            }

            var path = Path.Combine(_strategiesDirectory, $"{SelectedStrategyFilename}.txt");
            File.Delete(path);

            SelectedStrategyFilename = string.Empty;
            RefreshStrategyFilenames();
            UpdateStrategiesService();
        }

        private void UpdateStrategiesService()
        {
            _strategyService.ClearStrategies(false);
            foreach (var strategyFilename in StrategyFilenames)
            {
                var path = Path.Combine(_strategiesDirectory, $"{strategyFilename}.txt");

                if (File.Exists(path))
                {
                    var code = File.ReadAllText(path);
                    _strategyService.RegisterStrategy(code, false);
                }
            }

            _strategyService.RegisterStrategy(new TrendStrategy(), false);

            _strategyService.SetStrategiesToUseRiskSizing(false);
            _strategyService.NotifyStrategiesChanged();
        }

        private void LoadDefaultStrategyText()
        {
            var binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _defaultStrategyText = File.ReadAllText(Path.Combine(binPath, "DefaultStrategy.cs"));
        }

        private void RefreshStrategyFilenames()
        {
            StrategyFilenames.Clear();
            var strategyPaths = Directory.GetFiles(_strategiesDirectory, "*.txt");
            foreach (var strategyPath in strategyPaths)
            {
                StrategyFilenames.Add(Path.GetFileNameWithoutExtension(strategyPath));
            }
        }

        public ObservableCollection<string> StrategyFilenames { get; private set; } = new ObservableCollection<string>();

        private void CreateStrategy()
        {
            var res = InputView.Show();
            var path = Path.Combine(_strategiesDirectory, $"{res.Text}.txt");
            File.WriteAllText(path, _defaultStrategyText);

            RefreshStrategyFilenames();
            UpdateStrategiesService();
        }

        public TradesResultsViewModel ResultsViewModel { get; }

        public string SelectedStrategyFilename
        {
            get => _selectedStrategyFilename;
            set
            {
                Save();

                _selectedStrategyFilename = value;
                OnPropertyChanged();

                if (!string.IsNullOrEmpty(_selectedStrategyFilename))
                {
                    var code = File.ReadAllText(Path.Combine(_strategiesDirectory, $"{_selectedStrategyFilename}.txt"));
                    CodeText = code;
                }
                else
                {
                    CodeText = string.Empty;
                }

                UpdateStrategiesService();
            }
        }

        private void Save()
        {
            if (!string.IsNullOrEmpty(SelectedStrategyFilename))
            {
                var path = Path.Combine(_strategiesDirectory, $"{SelectedStrategyFilename}.txt");

                if (File.Exists(path))
                {
                    File.WriteAllText(path, CodeText);
                }
            }
        }

        public DelegateCommand SaveCommand { get; private set; }

        public DelegateCommand CreateStrategyCommand { get; private set; }

        public DelegateCommand DeleteStrategyCommand { get; private set; }

        public string CodeText
        {
            get => _codeText;
            set
            {
                _codeText = value;
                StrategyRunViewModel.CustomCode = _codeText;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}