using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Buffers.Text;
using System.Text;
using System.Text.Unicode;
using Vintagestory.API.Common;

namespace BloodyStory.Config
{
    public class ConfigManager<T> where T : new()
    {
        private readonly string ConfigFilename;
        private readonly string WorldConfigStringName;

        private bool logging;

        private ICoreAPI api;

        private T _modConfig;

        public T modConfig
        {
            get
            {
                if (_modConfig is null) { Reload(); }
                return _modConfig;
            }
            set
            {
                _modConfig = value;
            }
        }

        public ConfigManager(ICoreAPI api, string filename, bool logging = true)
        {
            ConfigFilename = filename;
            WorldConfigStringName = $"{filename}.config";
            this.api = api;
            this.logging = logging;

            Reload();
        }

        public void Reload()
        {
            string jsonConfig;
            switch (api.Side)
            {
                case (EnumAppSide.Client):
                    jsonConfig = api.World.Config.GetString(WorldConfigStringName);

                    if (jsonConfig != null)
                    {
                        Log("[{0}] got world config", new object[] { ConfigFilename });
                        jsonConfig = Encoding.UTF8.GetString(Convert.FromBase64String(jsonConfig));
                        _modConfig = JsonConvert.DeserializeObject<T>(jsonConfig);
                    }
                    else
                    {
                        if (logging) api.Logger.Error("[{0}] failed to acquire world config", new object[] { ConfigFilename });
                        // TODO: implement attempted re-acquisition of world config
                    }
                    break;

                case (EnumAppSide.Server):
                    Log("[{0}] trying to load config", new object[] { ConfigFilename });
                    _modConfig = api.LoadModConfig<T>($"{ConfigFilename}.json");
                    if (_modConfig == null)
                    {
                        Log("[{0}] generating new config", new object[] { ConfigFilename });
                        _modConfig = new();
                        api.StoreModConfig(_modConfig, $"{ConfigFilename}.json");
                    }
                    else Log("[{0}] config loaded", new object[] { ConfigFilename });

                    jsonConfig = JsonConvert.SerializeObject(_modConfig);
                    jsonConfig = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonConfig));
                    api.World.Config.SetString(WorldConfigStringName, jsonConfig);
                    Log("[{0}] set world config", new object[] { ConfigFilename });

                    break;
            }
        }

        private void Log(string message, object[] objects)
        {
            if (logging) api.Logger.Event(message, objects);
        }
    }
}
