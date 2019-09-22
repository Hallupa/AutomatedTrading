using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Abt.Controls.SciChart.Numerics.CoordinateCalculators;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using Hallupa.Library.UI.Views;
using Keras.Models;
using log4net;
using Numpy;
using Numpy.Models;
using TraderTools.AI;
using TraderTools.AutomatedTraderAI.Services;
using TraderTools.Basics;
using TraderTools.Core.Services;
using TraderTools.Core.UI.ViewModels;
using Model = TraderTools.AI.Model;

namespace TraderTools.AutomatedTraderAI.ViewModels
{
    public class TrainingViewModel : DoubleChartViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private MarketsService _marketsSetvice;
        [Import] private IBrokersService _brokersService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        private Market _selectedMarket;
        private Model _selectedModel;
        private ModelDataPoint _selectedModelDataPoint;

        public TrainingViewModel()
        {
            DependencyContainer.ComposeParts(this);

            Markets = new ObservableCollection<Market>(_marketsSetvice.GetMarkets().OrderBy(x => x.Name));
            AddModelDataCommand = new DelegateCommand(o => AddModelData());
            AddModelCommand = new DelegateCommand(o => AddModel());
            DeleteModelCommand = new DelegateCommand(o => DeleteModel());
            DeleteModelDataCommand = new DelegateCommand(o => DeleteModelData());
            GenerateDataCommand = new DelegateCommand(o => GenerateData());
            ChangeInputCountCommand = new DelegateCommand(o => ChangeInputCount());
            TrainCommand = new DelegateCommand(o => Train());
            TestModelCommand = new DelegateCommand(o => TestModel());
        }

        #region Properties
        [Import]
        public ModelsService ModelsService { get; set; }

        public DelegateCommand DeleteModelCommand { get; }

        public ObservableCollection<Market> Markets { get; }

        public DelegateCommand AddModelDataCommand { get; }

        public DelegateCommand TrainCommand { get; }

        public DelegateCommand GenerateDataCommand { get; }

        public DelegateCommand DeleteModelDataCommand { get; }

        public DelegateCommand ChangeInputCountCommand { get; }

        public DelegateCommand TestModelCommand { get; private set; }

        public Model SelectedModel
        {
            get => _selectedModel;
            set
            {
                _selectedModel = value;
                OnPropertyChanged();
            }
        }

        public DelegateCommand AddModelCommand { get; }

        public ModelDataPoint SelectedModelDataPoint
        {
            get => _selectedModelDataPoint;
            set
            {
                _selectedModelDataPoint = value;

                if (_selectedModelDataPoint == null) return;

                _selectedMarket = _marketsSetvice.GetMarkets().First(m => m.Name == _selectedModelDataPoint.Market);
                ShowMarket(_selectedMarket.Name, _selectedModelDataPoint.DateTime);
                var data = ChartViewModel.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries;
                ChartViewModel.XVisibleRange.SetMinMax(data.Count - _selectedModel.InputsCount, data.Count - 1);

                data = ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries;
                ChartViewModelSmaller1.XVisibleRange.SetMinMax(data.Count - _selectedModel.InputsCount, data.Count - 1);

                OnPropertyChanged("SelectedMarket");
            }
        }

        public Market SelectedMarket
        {
            get => _selectedMarket;
            set
            {
                _selectedMarket = value;

                if (_selectedMarket != null)
                {
                    ShowMarket(_selectedMarket.Name);
                }

                OnPropertyChanged();
            }
        }
        #endregion

        private void ShowMarket(string market, DateTime? uptoDateTime = null)
        {
            var broker = _brokersService.GetBroker("FXCM");
            var largeChartCandles = _candlesService.GetCandles(broker, market, LargeChartTimeframe, false, cacheData: false, maxCloseTimeUtc: uptoDateTime);
            var smallChartCandles = _candlesService.GetCandles(broker, market, SmallChartTimeframe, false, maxCloseTimeUtc: uptoDateTime);

            ViewCandles(
                _selectedMarket.Name,
                SmallChartTimeframe,
                smallChartCandles,
                LargeChartTimeframe,
                largeChartCandles);
        }

        private void ChangeInputCount()
        {
            if (SelectedModel == null) return;

            var res = InputView.Show("Inputs count?");

            if (res.OKClicked && int.TryParse(res.Text, out var value))
            {
                SelectedModel.InputsCount = value;
                ModelsService.SaveModels();
                OnPropertyChanged("Models");
            }
        }

        private void GenerateData()
        {
            if (SelectedModel == null) return;
            var dataGenerator = new DataGenerator();
            dataGenerator.CreateData(SelectedModel);
        }

        private void Train()
        {
            if (SelectedModel == null) return;

            var script = "ImageTest.py";
            var directory = DataGenerator.GetModelDirectory(SelectedModel, _dataDirectoryService);
            var outputs = SelectedModel.DataPoints.Select(x => x.Label).Distinct().Count() + 1;

            var scriptDrectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Tensorflow");
            Directory.SetCurrentDirectory(scriptDrectory);

            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = scriptDrectory,
                Arguments = $"{script} \"{directory}\" {outputs}",
                FileName = "RunScript.bat" // @"C:\Users\Oliver Wickenden\AppData\Local\Programs\Python\Python37\python.exe"
            };

            startInfo.UseShellExecute = false;
           // startInfo.RedirectStandardOutput = true;

            Log.Info("Running Python script");
            Log.Info("-------------------------------------");
            Process.Start(startInfo);
            /*using (var process = Process.Start(startInfo))
            {   
                using (var reader = process.StandardOutput)
                {
                    var result = reader.ReadToEnd();
                    Log.Info(result);
                }
            }*/
            Log.Info("-------------------------------------");

            //var p = Process.Start(startInfo);
            //if (p != null && !p.HasExited)
            //     p.WaitForExit();
        }

        private void DeleteModelData()
        {
            if (SelectedModelDataPoint == null) return;
            if (SelectedModel == null) return;

            SelectedModel.DataPoints.Remove(SelectedModelDataPoint);
            ModelsService.SaveModels();
        }

        private void AddModelData()
        {
            if (ChartViewModelSmaller1.ChartPaneViewModels.Count == 0) return;
            if (SelectedModel == null) return;

            var res = InputView.Show("Enter label");
            if (!res.OKClicked) return;
            var label = res.Text;

            var maxIndexShown = ChartViewModelSmaller1.XVisibleRange.Max;
            var dataSeries = ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries;
            var latestDate = ((ICategoryCoordinateCalculator)dataSeries.ParentSurface.XAxis.GetCurrentCoordinateCalculator()).TransformIndexToData(maxIndexShown);

            var dataPoint = new ModelDataPoint
            {
                Market = SelectedMarket.Name,
                DateTime = latestDate,
                Label = label
            };

            SelectedModel.DataPoints.Add(dataPoint);
            ModelsService.SaveModels();
        }

        private void DeleteModel()
        {
            if (SelectedModel == null) return;

            if (MessageBox.Show("Are you sure?", "Confirm delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                ModelsService.Models.Remove(SelectedModel);
                ModelsService.SaveModels();
            }
        }

        private void AddModel()
        {
            var res = InputView.Show("Enter model name");
            if (!res.OKClicked) return;
            var name = res.Text;

            res = InputView.Show("Enter inputs count");
            if (!res.OKClicked) return;
            var inputsCount = int.Parse(res.Text);

            res = InputView.Show("Enter model data type. 1 = EMAs and candles, 2 = EMAs only, 3 = Candles relative to 8EMA");
            if (!res.OKClicked) return;
            var modelDataType = (ModelDataType)int.Parse(res.Text);

            var model = new Model
            {
                Name = name,
                InputsCount = inputsCount,
                ModelDataType = modelDataType
            };

            ModelsService.Models.Add(model);
            ModelsService.SaveModels();
        }

        private void TestModel()
        {
            if (SelectedModel == null) return;

            ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Clear();
            var directory = DataGenerator.GetModelDirectory(SelectedModel, _dataDirectoryService);
            var path = Path.Combine(directory, "model.h5");
            var model = BaseModel.LoadModel(path);


            var dataGenerator = new DataGenerator();
            /*dataGenerator.CreateData(
                SelectedMarket.Name,
                ModelDataType.EMAsOnly,
                new DateTime(2019, 6, 11, 15, 0, 0, DateTimeKind.Utc),
                SelectedModel.InputsCount,
                out var imgWidth,
                out var imgHeight,
                out var imgData,
                out var rawData);*/

            var candlesAndIndicators = dataGenerator.GetCandlesWithIndicators(SelectedMarket.Name, new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50 });

            var currentClassificationIndex = -1;
            var startIndex = 80;
            var minClassification = 0.9;
            var maxYForAnnotations = candlesAndIndicators.Max(x => x.Candle.HighBid) * 1.05;
            var minYForAnnotations = candlesAndIndicators.Min(x => x.Candle.LowBid) * 0.95;
            for (var uptoIndex = startIndex; uptoIndex <= candlesAndIndicators.Count - 1; uptoIndex++)
            {
                dataGenerator.CreateData(candlesAndIndicators, uptoIndex, ModelDataType.EMAsOnly, SelectedModel.InputsCount, out _, out _, out _, out var rawData);

                var x = np.array(np.array(rawData)).reshape(new Shape(1, DataGenerator.GetDataPointsCount(SelectedModel.ModelDataType) * SelectedModel.InputsCount));

                var y = model.Predict(x)[0];

                // Get which index is highest
                var maxIndex = 0;
                for (var i = 1; i < y.size; i++)
                {
                    if ((float)y[i] > (float)y[maxIndex]) maxIndex = i;
                }

                var classificationIndex = maxIndex;
                var classificationConfidence = (float)y[maxIndex];

                // Look for end of section
                if (currentClassificationIndex != -1 && (currentClassificationIndex != classificationIndex || classificationConfidence < minClassification))
                {
                    Brush color = null;
                    var opacity = 0.2;
                    switch (currentClassificationIndex)
                    {
                        case 1:
                            // Down
                            color = Brushes.DarkRed;
                            opacity = 0.6;
                            break;
                        case 2:
                            color = Brushes.Goldenrod;
                            opacity = 0.6;
                            break;
                        case 3:
                            color = Brushes.Aqua;
                            break;
                        case 4:
                            color = Brushes.Blue;
                            break;
                        case 5:
                            // Up
                            color = Brushes.LimeGreen;
                            opacity = 0.6;
                            break;
                    }

                    if (color != null)
                    {
                        var annotation = new BoxAnnotation
                        {
                            X1 = startIndex,
                            X2 = uptoIndex,
                            Y1 = maxYForAnnotations,
                            Y2 = minYForAnnotations,
                            Background = color,
                            Opacity = opacity
                        };
                        ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Add(annotation);
                    }
                }

                if (classificationConfidence < minClassification)
                {
                    currentClassificationIndex = -1;
                }
                else if (currentClassificationIndex != classificationIndex)
                {
                    currentClassificationIndex = classificationIndex;
                    startIndex = uptoIndex;
                }
            }
        }
    }
}