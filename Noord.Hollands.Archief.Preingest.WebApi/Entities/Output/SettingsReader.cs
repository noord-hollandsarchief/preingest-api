using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Newtonsoft.Json;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using System.Collections;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Output
{
    public class SettingsReader
    {
        private PreingestActionModel _model = null;
        public SettingsReader(String dataFolder, Guid guid, String filename = "SettingsHandler.json")
        {
            String jsonFile = Path.Combine(dataFolder, guid.ToString(), filename);
            if (File.Exists(jsonFile))
            {
                string jsonContent = File.ReadAllText(jsonFile);
                _model = JsonConvert.DeserializeObject<PreingestActionModel>(jsonContent);
            }
        }

        public BodySettings GetSettings()
        {
            if (_model == null)
                return null;

            if (_model.ActionData == null)
                return null;

            BodySettings settings = JsonConvert.DeserializeObject<BodySettings>(_model.ActionData.ToString());
            return settings;
        }

        public T GetSettings<T>()
        {
            if (_model == null)
                return default(T);

            if (_model.ActionData == null)
                return default(T);

            T settings = JsonConvert.DeserializeObject<T>(_model.ActionData.ToString());
            return settings;
        }
    }
}
