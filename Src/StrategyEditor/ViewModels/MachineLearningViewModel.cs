using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using log4net;
using Newtonsoft.Json;
using StrategyEditor.ML;
using StrategyEditor.Views;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Extensions;
using TraderTools.Core.UI;
using TraderTools.Core.UI.Services;

namespace StrategyEditor.ViewModels
{
    public class MachineLearningViewModel : DependencyObject
    {
        [Import] private IDataDirectoryService _dataDirectoryService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IBrokersService _brokersService;
        private IDisposable _chartClickedDisposable;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MachineLearningViewModel()
        {
            _dispatcher = Dispatcher;
            DependencyContainer.ComposeParts(this);
            CreatePointCommand = new DelegateCommand(o => CreatePoint());
            ViewPointCommand = new DelegateCommand(o => ViewPoint());
            CreatePointSetCommand = new DelegateCommand(o => CreatePointSet());
            DeletePointSetCommand = new DelegateCommand(o => DeletePointSet());
            DeletePointCommand = new DelegateCommand(o => DeletePoint());
            TrainCommand = new DelegateCommand(o => Train(), o => IsTrainingEnabled);
            TestCommand = new DelegateCommand(o => TestModel(), o => IsTestEnabled);
            MLPointDoubleClickComamnd = new DelegateCommand(o => MLPointDoubleClicked());
            Chart = new MachineLearningChartViewModel();
            _chartClickedDisposable = ChartingService.ChartClickObservable.Subscribe(ChartClicked);
            Load();
        }

        public DelegateCommand MLPointDoubleClickComamnd { get; }

        public DelegateCommand ViewPointCommand { get; }

        public DelegateCommand DeletePointCommand { get; }

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
        public bool IsTestEnabled { get; set; } = true;
        public ObservableCollection<MLPointCollection> MLPointSets { get; } = new ObservableCollection<MLPointCollection>();

        public static readonly DependencyProperty SelectedMLPointsSetProperty = DependencyProperty.Register(
            "SelectedMLPointsSet", typeof(MLPointCollection), typeof(MachineLearningViewModel), new PropertyMetadata(default(MLPointCollection)));

        private MLPoint _pointCreating;
        private Trainer _trainer;
        private Dispatcher _dispatcher;

        public MLPointCollection SelectedMLPointsSet
        {
            get { return (MLPointCollection)GetValue(SelectedMLPointsSetProperty); }
            set { SetValue(SelectedMLPointsSetProperty, value); }
        }

        public static readonly DependencyProperty SelectedMLPointProperty = DependencyProperty.Register(
            "SelectedMLPoint", typeof(MLPoint), typeof(MachineLearningViewModel), new PropertyMetadata(default(MLPoint)));

        public MLPoint SelectedMLPoint
        {
            get { return (MLPoint) GetValue(SelectedMLPointProperty); }
            set { SetValue(SelectedMLPointProperty, value); }
        }

        public DelegateCommand CreatePointCommand { get; }
        public DelegateCommand CreatePointSetCommand { get; }
        public DelegateCommand DeletePointSetCommand { get; }

        private void MLPointDoubleClicked()
        {
            ViewPoint();
        }

        private void Train()
        {
            if (SelectedMLPointsSet == null) return;

            IsTrainingEnabled = false;
            TrainCommand.RaiseCanExecuteChanged();
            _trainer = new Trainer(_candlesService, _brokersService);
            var pointsSet = SelectedMLPointsSet;

            _trainer.TrainAsync(pointsSet)
                .ContinueWith(o =>
                {
                    _dispatcher.Invoke(() =>
                    {
                        IsTrainingEnabled = true;
                        TrainCommand.RaiseCanExecuteChanged();
                    });
                });
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

        private void ViewPoint()
        {
            if (SelectedMLPoint == null || SelectedMLPointsSet == null) return;

            var dataGenerator = new DataGenerator(_candlesService, _brokersService);
            var xy = dataGenerator.GetPointXYData(SelectedMLPoint, SelectedMLPointsSet);

            Log.Info($"Point X data: {string.Join(", ", xy.x.Select(v => $"{v:0.00}"))}");

            Chart.ChartViewModel.ChartPaneViewModels.Clear();
            ChartHelper.SetChartViewModelPriceData(xy.candlesUsed, Chart.ChartViewModel, "ML point");
        }

        private void TestModel()
        {
            if (_trainer == null) return;

            IsTestEnabled = false;
            TestCommand.RaiseCanExecuteChanged();

            var candles = _candlesService.GetDerivedCandles(
                _brokersService.GetBroker(SelectedMLPointsSet.Broker),
                Chart.Market,
                Chart.SelectedTimeframe);

            if (SelectedMLPointsSet.UseHeikenAshi) candles = candles.CreateHeikinAshiCandles();

            var w = _trainer.TestAsync(candles)
                .ContinueWith(o =>
                {
                    var results = o.Result;
                    _dispatcher.Invoke(() =>
                    {
                        IsTestEnabled = true;
                        TestCommand.RaiseCanExecuteChanged();

                        if (Chart.ChartViewModel.ChartPaneViewModels.Count == 0) return;

                        if (Chart.ChartViewModel.ChartPaneViewModels[0].TradeAnnotations == null)
                        {
                            Chart.ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();
                        }

                        var annotations = new AnnotationCollection();
                        foreach (var r in results)
                        {
                            ChartHelper.AddBuySellMarker(
                                r.Direction, annotations, null, 
                                new DateTime(r.DateTime, DateTimeKind.Utc), r.Price,
                                true, true);
                        }

                        Chart.ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = annotations;
                    });
                });
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

        private void DeletePoint()
        {
            if (SelectedMLPointsSet == null || SelectedMLPoint == null) return;

            SelectedMLPointsSet.Points.Remove(SelectedMLPoint);
            Save();
        }
    }

    public class MLPointCollection
    {
        public string Name { get; set; }
        public ObservableCollection<MLPoint> Points { get; set; } = new ObservableCollection<MLPoint>();
        public bool UseHeikenAshi { get; set; }
        public string Broker { get; set; }
        public bool GenerateExtraPoints { get; set; } = true;
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