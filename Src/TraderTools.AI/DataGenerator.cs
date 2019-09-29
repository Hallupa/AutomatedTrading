using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Hallupa.Library;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.AI
{
    public class DataGenerator
    {
        [Import] private IBrokersService _brokersService;
        [Import] private IBrokersCandlesService _candlesService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        private IBroker _broker;

        public DataGenerator()
        {
            DependencyContainer.ComposeParts(this);
            _broker = _brokersService.Brokers.First(x => x.Name == "FXCM");
        }

        /*public void CreateData(string market, ModelDataType modelDataType, DateTime dateTime, int numberOfCandles,
            out int imgWidth, out int imgHeight, out byte[,] imgArray)
        {
            var candlesWithIndicators = GetCandlesWithIndicators(market, new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50 }, dateTime);

            CreateData(candlesWithIndicators, candlesWithIndicators.Count - 1, modelDataType, numberOfCandles, out imgWidth, out imgHeight, out imgArray);
        }*/

        public void CreateRawData(string market, ModelDataType modelDataType, DateTime dateTime, int numberOfCandles, out float[] rawData)
        {
            var candlesWithIndicators = GetCandlesWithIndicators(market, new[] { Indicator.EMA8, Indicator.EMA25, Indicator.EMA50 }, dateTime);

            CreateRawData(candlesWithIndicators, candlesWithIndicators.Count - 1, modelDataType, numberOfCandles, out rawData);
        }

        public static int GetDataPointsCount(ModelDataType modelDataType)
        {
            var ret = 0;
            if (modelDataType.HasFlag(ModelDataType.EMA8)) ret++;
            if (modelDataType.HasFlag(ModelDataType.EMA25)) ret++;
            if (modelDataType.HasFlag(ModelDataType.EMA50)) ret++;
            if (modelDataType.HasFlag(ModelDataType.Candles)) ret += 4;

            return ret;
        }

        public void CreateRawData(List<CandleAndIndicators> candlesWithIndicators, int uptoIndex, IModelDetails m, out float[] rawData)
        {
            CreateRawData(candlesWithIndicators, uptoIndex, m.ModelDataType, m.InputsCount, out rawData);
        }

        public void CreateRawData(List<CandleAndIndicators> candlesWithIndicators, int uptoIndex, ModelDataType modelDataType, int numberOfCandles, out float[] rawData)
        {
            var rawDataList = new List<float>();

            var prevC = candlesWithIndicators[uptoIndex - numberOfCandles];
            var yMax = float.MinValue;
            var yLow = float.MaxValue;

            var expectedValues = 0;
            if (modelDataType.HasFlag(ModelDataType.EMA8)) expectedValues += numberOfCandles;
            if (modelDataType.HasFlag(ModelDataType.EMA25)) expectedValues += numberOfCandles;
            if (modelDataType.HasFlag(ModelDataType.EMA50)) expectedValues += numberOfCandles;
            if (modelDataType.HasFlag(ModelDataType.Candles)) expectedValues += numberOfCandles * 4;

            var first = true;
            for (var i = uptoIndex - numberOfCandles + 1; i <= uptoIndex; i++)
            {
                if (modelDataType.HasFlag(ModelDataType.EMA8))
                { 
                    if (first || candlesWithIndicators[i][Indicator.EMA8].Value > yMax) yMax = candlesWithIndicators[i][Indicator.EMA8].Value;
                    if (first || candlesWithIndicators[i][Indicator.EMA8].Value < yLow) yLow = candlesWithIndicators[i][Indicator.EMA8].Value;
                }

                if (modelDataType.HasFlag(ModelDataType.EMA25))
                {
                    if (first || candlesWithIndicators[i][Indicator.EMA25].Value > yMax) yMax = candlesWithIndicators[i][Indicator.EMA25].Value;
                    if (first || candlesWithIndicators[i][Indicator.EMA25].Value < yLow) yLow = candlesWithIndicators[i][Indicator.EMA25].Value;
                }

                if (modelDataType.HasFlag(ModelDataType.EMA50))
                {
                    if (first || candlesWithIndicators[i][Indicator.EMA50].Value > yMax) yMax = candlesWithIndicators[i][Indicator.EMA50].Value;
                    if (first || candlesWithIndicators[i][Indicator.EMA50].Value < yLow) yLow = candlesWithIndicators[i][Indicator.EMA50].Value;
                }

                if (modelDataType.HasFlag(ModelDataType.Candles))
                {
                    if (first || candlesWithIndicators[i].Candle.HighBid > yMax) yMax = candlesWithIndicators[i].Candle.HighBid;
                    if (first || candlesWithIndicators[i].Candle.LowBid < yLow) yLow = candlesWithIndicators[i].Candle.LowBid;
                }

                first = false;
            }

            yMax *= 1.005F;
            yLow *= 0.995F;

            Func<float, float> convertToRaw = y => (y - yLow) / (yMax - yLow);

            for (var i = 0; i < numberOfCandles; i++)
            {
                var c = candlesWithIndicators[uptoIndex - numberOfCandles + i + 1];

                if (modelDataType.HasFlag(ModelDataType.Candles))
                {
                    rawDataList.Add(convertToRaw(c.Candle.HighBid));
                    rawDataList.Add(convertToRaw(c.Candle.LowBid));
                    rawDataList.Add(convertToRaw(c.Candle.OpenBid));
                    rawDataList.Add(convertToRaw(c.Candle.CloseBid));
                }

                if (modelDataType.HasFlag(ModelDataType.EMA8)) rawDataList.Add(convertToRaw(c[Indicator.EMA8].Value));

                if (modelDataType.HasFlag(ModelDataType.EMA25)) rawDataList.Add(convertToRaw(c[Indicator.EMA25].Value));
                if (modelDataType.HasFlag(ModelDataType.EMA50)) rawDataList.Add(convertToRaw(c[Indicator.EMA50].Value));
            }

            rawData = rawDataList.ToArray();

            if (rawData.Length != expectedValues)
            {
                throw new ApplicationException($"Actual values: {rawData.Length} does not equal expected values {expectedValues}");
            }
        }

        /*public void CreateData(List<CandleAndIndicators> candlesWithIndicators, int uptoIndex, ModelDataType modelDataType, int numberOfCandles,
            out int imgWidth, out int imgHeight, out byte[,] imgArray)
        {
            imgWidth = numberOfCandles * 3;
            imgHeight = modelDataType == ModelDataType.EMAsAndCandles ? 100 : 60;
            byte closeUpColor = 1;
            byte closeDownColor = 2;

            var prevC = candlesWithIndicators[uptoIndex - numberOfCandles];
            var yMax = float.MinValue;
            var yLow = float.MaxValue;

            var first = true;
            for (var i = uptoIndex - numberOfCandles + 1; i <= uptoIndex; i++)
            {
                if (first || candlesWithIndicators[i][Indicator.EMA8].Value > yMax) yMax = candlesWithIndicators[i][Indicator.EMA8].Value;
                if (first || candlesWithIndicators[i][Indicator.EMA25].Value > yMax) yMax = candlesWithIndicators[i][Indicator.EMA25].Value;
                if (first || candlesWithIndicators[i][Indicator.EMA50].Value > yMax) yMax = candlesWithIndicators[i][Indicator.EMA50].Value;

                if (first || candlesWithIndicators[i][Indicator.EMA8].Value < yLow) yLow = candlesWithIndicators[i][Indicator.EMA8].Value;
                if (first || candlesWithIndicators[i][Indicator.EMA25].Value < yLow) yLow = candlesWithIndicators[i][Indicator.EMA25].Value;
                if (first || candlesWithIndicators[i][Indicator.EMA50].Value < yLow) yLow = candlesWithIndicators[i][Indicator.EMA50].Value;

                if (modelDataType == ModelDataType.EMAsAndCandles)
                {
                    if (first || candlesWithIndicators[i].Candle.HighBid > yMax) yMax = candlesWithIndicators[i].Candle.HighBid;
                    if (first || candlesWithIndicators[i].Candle.LowBid < yLow) yLow = candlesWithIndicators[i].Candle.LowBid;
                }

                first = false;
            }

            yMax *= 1.005F;
            yLow *= 0.995F;

            var factor = (float)(imgHeight - 1) / (float)(yMax - yLow);
            Func<float, int> convertY = y => (int)((y - yLow) * factor);
            imgArray = new byte[imgHeight, numberOfCandles * 3];

            for (var i = 0; i < numberOfCandles; i++)
            {
                var c = candlesWithIndicators[uptoIndex - numberOfCandles + i + 1];
                var candleMidX = i * 3 + 1;

                var yClose = convertY(c.Candle.CloseBid);
                var yOpen = convertY(c.Candle.OpenBid);
                var y1High = convertY(c.Candle.HighBid);
                var y2Low = convertY(c.Candle.LowBid);

                if (modelDataType == ModelDataType.EMAsAndCandles)
                {
                    Rect(imgArray, imgWidth, imgHeight, c.Candle.CloseBid > c.Candle.OpenBid ? closeUpColor : closeDownColor, candleMidX - 1, yClose, candleMidX + 1, yOpen);
                    Rect(imgArray, imgWidth, imgHeight, c.Candle.CloseBid > c.Candle.OpenBid ? closeUpColor : closeDownColor, candleMidX, y1High, candleMidX, y2Low);
                }

                Line(imgArray, imgWidth, imgHeight, 3, candleMidX - 3, convertY(prevC[Indicator.EMA50].Value), candleMidX, convertY(c[Indicator.EMA50].Value));
                Line(imgArray, imgWidth, imgHeight, 4, candleMidX - 3, convertY(prevC[Indicator.EMA25].Value), candleMidX, convertY(c[Indicator.EMA25].Value));
                Line(imgArray, imgWidth, imgHeight, 5, candleMidX - 3, convertY(prevC[Indicator.EMA8].Value), candleMidX, convertY(c[Indicator.EMA8].Value));

                prevC = c;
            }
        }*/

        public void CreateData(IModelDetails model)
        {
            var dpNum = 0;

            var modelDirectory = GetModelDirectory(model, _dataDirectoryService);
            Directory.GetFiles(modelDirectory, "*.*", SearchOption.TopDirectoryOnly).ToList().ForEach(File.Delete);

            for (var dpIndex = 1; dpIndex < model.DataPoints.Count; dpIndex++)
            {
                var dp = model.DataPoints[dpIndex];
                dpNum++;

                // CreateData(dp.Market, model.ModelDataType, dp.DateTime, model.InputsCount, out var imgWidth, out var imgHeight, out var imgArray);
                CreateRawData(dp.Market, model.ModelDataType, dp.DateTime, model.InputsCount, out var rawData);

                var path = Path.Combine(modelDirectory, $"{dp.Label}_{dpNum}.png");
                // SaveImage(path, imgArray, imgWidth, imgHeight);
                path = Path.Combine(modelDirectory, $"{dp.Label}_{dpNum}.csv");
                SaveRawDataAndLabel(path, rawData, dp.LabelValue);
            }
        }

        public static string GetModelDirectory(IModelDetails model, IDataDirectoryService dataDirectoryService)
        {
            return Path.Combine(dataDirectoryService.MainDirectoryWithApplicationName, "Models", model.Name);
        }

        private void SaveRawDataAndLabel(string path, float[] data, int label)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var fileData = string.Join(",", new[] {label.ToString()}.Concat(data.Select(x => x.ToString())));
            File.WriteAllText(path, fileData);
        }

        private void SaveImage(string path, byte[,] imgArray, int width, int height)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var bmp = new Bitmap(width, height);
            var colourMap = new Dictionary<byte, Color>
            {
                {0,  Color.FromArgb(255, 255, 255, 255)},
                {1,  Color.FromArgb(255, 0, 100, 0)},
                {2,  Color.FromArgb(255, 200, 50, 50)},
                {3,  Color.FromArgb(255, 50, 50, 200)},
                {4,  Color.FromArgb(255, 255, 100, 0)},
                {5,  Color.FromArgb(255, 200, 200, 0)}
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    bmp.SetPixel(x, height - y - 1, colourMap[imgArray[y, x]]);
                }
            }

            bmp.Save(path, ImageFormat.Png);
        }

        private void Line(byte[,] imgArray, int w, int h, byte value, int x1, int y1, int x2, int y2)
        {
            if (x2 < x1)
            {
                var tmp = x2;
                x2 = x1;
                x1 = tmp;
                tmp = y2;
                y2 = y1;
                y1 = tmp;
            }

            var r = (y2 - y1) / (float)(x2 - x1);
            var yPrev = -1;
            for (var x = x1; x <= x2; x++)
            {
                var endY = (int)(y1 + (x - x1) * r);

                if (yPrev < endY)
                {
                    for (var y = yPrev == -1 ? endY : yPrev; y <= endY; y++)
                    {
                        if (x >= 0 && y >= 0 && x < w && y < h)
                        {
                            imgArray[y, x] = value;
                        }
                    }
                }
                else
                {
                    for (var y = endY; y <= yPrev; y++)
                    {
                        if (x >= 0 && y >= 0 && x < w && y < h)
                        {
                            imgArray[y, x] = value;
                        }
                    }
                }

                yPrev = endY;
            }
        }

        private void Rect(byte[,] imgArray, int w, int h, byte value, int x1, int y1, int x2, int y2)
        {
            if (y1 > y2)
            {
                var tmpY = y1;
                y1 = y2;
                y2 = tmpY;
            }

            for (var x = x1; x <= x2; x++)
            {
                for (var y = y1; y <= y2; y++)
                {
                    if (x >= 0 && y >= 0 && x < w && y < h)
                    {
                        imgArray[y, x] = value;
                    }
                }
            }
        }

        public List<CandleAndIndicators> GetCandlesWithIndicators(string market, Indicator[] indicators, DateTime? dateTime = null)
        {
            var candles = _candlesService.GetCandles(_broker, market, Timeframe.D1, false, null, maxCloseTimeUtc: dateTime);

            var ret = new List<CandleAndIndicators>();
            var maxIndicators = indicators.Select(x => (int)x).Max() + 1;
            var indicatorCalculators = new List<(Indicator Indicator, IIndicator IndicatorCalculator)>();

            foreach (var indicator in indicators)
            {
                indicatorCalculators.Add((indicator, IndicatorsHelper.CreateIndicator(indicator)));
            }

            foreach (var candle in candles)
            {
                var candleAndIndicators = new CandleAndIndicators(candle, maxIndicators);

                foreach (var indicatorCalculator in indicatorCalculators)
                {
                    var signalAndValue = indicatorCalculator.IndicatorCalculator.Process(candle);
                    candleAndIndicators.Indicators[(int)indicatorCalculator.Indicator] = signalAndValue;
                }

                ret.Add(candleAndIndicators);
            }

            return ret;
        }
    }
}