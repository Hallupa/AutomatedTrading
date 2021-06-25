using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Hallupa.Library;
using Newtonsoft.Json;
using StrategyEditor.ML;
using StrategyEditor.Views;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.UI.Services;

namespace StrategyEditor.ViewModels
{
    public class MachineLearningViewModel : DependencyObject
    {
        [Import] private IDataDirectoryService _dataDirectoryService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IBrokersService _brokersService;
        private IDisposable _chartClickedDisposable;

        public MachineLearningViewModel()
        {
            DependencyContainer.ComposeParts(this);
            CreatePointCommand = new DelegateCommand(o => CreatePoint());
            CreatePointSetCommand = new DelegateCommand(o => CreatePointSet());
            DeletePointSetCommand = new DelegateCommand(o => DeletePointSet());
            TrainCommand = new DelegateCommand(o => Train(), o => IsTrainingEnabled);
            TestCommand = new DelegateCommand(o => TestModel());
            Chart = new MachineLearningChartViewModel();
            _chartClickedDisposable = ChartingService.ChartClickObservable.Subscribe(ChartClicked);
            Load();
        }

        public DelegateCommand TestCommand { get; }

        public DelegateCommand TrainCommand { get; }

        public static readonly DependencyProperty CreatingPointProperty = DependencyProperty.Register(
            "CreatingPoint", typeof(bool?), typeof(MachineLearningViewModel), new PropertyMetadata(false));

        public bool? CreatingPoint
        {
            get { return (bool?)GetValue(CreatingPointProperty); }
            set { SetValue(CreatingPointProperty, value); }
        }

        [Import] public ChartingService ChartingService { get; set; }

        public MachineLearningChartViewModel Chart { get; }
        public bool IsTrainingEnabled { get; set; } = true;
        public ObservableCollection<MLPointCollection> MLPointSets { get; } = new ObservableCollection<MLPointCollection>();

        public static readonly DependencyProperty SelectedMLPointsSetProperty = DependencyProperty.Register(
            "SelectedMLPointsSet", typeof(MLPointCollection), typeof(MachineLearningViewModel), new PropertyMetadata(default(MLPointCollection)));

        private MLPoint _pointCreating;
        private Trainer _trainer;

        public MLPointCollection SelectedMLPointsSet
        {
            get { return (MLPointCollection)GetValue(SelectedMLPointsSetProperty); }
            set { SetValue(SelectedMLPointsSetProperty, value); }
        }

        public DelegateCommand CreatePointCommand { get; }
        public DelegateCommand CreatePointSetCommand { get; }
        public DelegateCommand DeletePointSetCommand { get; }

        private void Train()
        {
            if (SelectedMLPointsSet == null) return;

            IsTrainingEnabled = false;
            TrainCommand.RaiseCanExecuteChanged();
            _trainer = new Trainer(_candlesService, _brokersService);
            var pointsSet = SelectedMLPointsSet;

            //Task.Run(() =>
           // {
                _trainer.Train(pointsSet);

               // Dispatcher.Invoke(() =>
               // {
                    IsTrainingEnabled = true;
                    TrainCommand.RaiseCanExecuteChanged();
              //  });
           // });
        }

        private void ChartClicked((DateTime Time, double Price, Action setIsHandled) o)
        {
            if (CreatingPoint == true && _pointCreating != null)
            {
                _pointCreating.DateTimeUtc = o.Time;
                _pointCreating.Market = Chart.Market;
                _pointCreating.Timeframe = Chart.SelectedTimeframe;

                SelectedMLPointsSet.Points.Add(_pointCreating);
                Save();
                _pointCreating = null;
                CreatingPoint = false;
            }
        }

        private void TestModel()
        {
            if (_trainer == null) return;

            var candles = _candlesService.GetDerivedCandles(
                _brokersService.GetBroker(SelectedMLPointsSet.Broker),
                Chart.Market,
                Chart.SelectedTimeframe);

            if (SelectedMLPointsSet.UseHeikenAshi) candles = candles.CreateHeikinAshiCandles();

            var results = _trainer.Test(candles);
        }

        private void CreatePoint()
        {
            if (SelectedMLPointsSet == null || CreatingPoint == false) return;

            var view = new CreatePointView();
            view.ShowDialog();

            if (!view.Model.Ok) return;

            _pointCreating = new MLPoint
            {
                PointType = view.Model.SelectedOption
            };
        }

        private void Save()
        {
            File.WriteAllText(
                Path.Combine(
                    _dataDirectoryService.MainDirectoryWithApplicationName, "MLPoints.json"),
                JsonConvert.SerializeObject(MLPointSets.ToList()));
        }

        private void Load()
        {
            var path = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "MLPoints.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var d = JsonConvert.DeserializeObject<List<MLPointCollection>>(json);

            foreach (var z in d)
            {
                MLPointSets.Add(z);
            }

            SelectedMLPointsSet = MLPointSets.FirstOrDefault();
        }

        private void CreatePointSet()
        {
            var m = new MLPointCollection
            {
                Broker = "Binance"
            };

            var view = new CreatePointGroupView { DataContext = m };
            view.ShowDialog();

            if (!view.OKClicked) return;

            MLPointSets.Add(m);
            SelectedMLPointsSet = m;
            Save();
        }

        private void DeletePointSet()
        {
            if (SelectedMLPointsSet == null) return;

            MLPointSets.Remove(SelectedMLPointsSet);
            SelectedMLPointsSet = MLPointSets.FirstOrDefault();
            Save();
        }
    }

    public class MLPointCollection
    {
        public string Name { get; set; }
        public ObservableCollection<MLPoint> Points { get; set; } = new ObservableCollection<MLPoint>();
        public bool UseHeikenAshi { get; set; }
        public string Broker { get; set; }
    }

    public class MLPoint
    {
        public DateTime DateTimeUtc { get; set; }
        public string Market { get; set; }
        public MLPointType PointType { get; set; }
        public Timeframe Timeframe { get; set; }
    }

    public enum MLPointType
    {
        Buy,
        Sell,
        Hold
    }
}