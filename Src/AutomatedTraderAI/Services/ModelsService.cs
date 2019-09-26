using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Hallupa.Library.UI;
using Newtonsoft.Json;
using TraderTools.AI;
using TraderTools.Basics;

namespace TraderTools.AutomatedTraderAI.Services
{
    [Export(typeof(ModelsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ModelsService
    {
        private IDataDirectoryService _dataDirectoryService;

        [ImportingConstructor]
        public ModelsService(IDataDirectoryService dataDirectoryService)
        {
            _dataDirectoryService = dataDirectoryService;

            LoadModels();
        }

        public ObservableCollectionEx<Model> Models { get; } = new ObservableCollectionEx<Model>();

        private void LoadModels()
        {
            var saveName = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelData.json");
            if (!File.Exists(saveName)) return;

            var models = JsonConvert.DeserializeObject<List<Model>>(File.ReadAllText(saveName));

            // Upgrade models
            var updated = false;
            foreach (var model in models)
            {
                if (model.DataPoints.Any(x => x.LabelValue == 0))
                {
                    updated = true;
                    var lookup = new Dictionary<string, int>();
                    var nextValue = 1;
                    foreach (var dp in model.DataPoints)
                    {
                        if (!lookup.ContainsKey(dp.Label))
                        {
                            lookup[dp.Label] = nextValue;
                            nextValue++;
                        }

                        dp.LabelValue = lookup[dp.Label];
                    }
                }
            }

            Models.Clear();
            Models.AddRange(models);

            if (updated) SaveModels();
        }

        public void SaveModels()
        {
            var saveName = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelData.json");
            var saveNameTemp = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelData.temp");
            var oldName = Path.Combine(_dataDirectoryService.MainDirectoryWithApplicationName, "ModelDataOld.json");

            if (File.Exists(saveNameTemp)) File.Delete(saveNameTemp);

            var json = JsonConvert.SerializeObject(Models.ToList());
            File.WriteAllText(saveNameTemp, json);

            if (File.Exists(oldName)) File.Delete(oldName);
            if (File.Exists(saveName)) File.Move(saveName, oldName);

            File.Move(saveNameTemp, saveName);
        }
    }
}