using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace BloodyStory.Config
{
    public class ConfigManager<T> where T : new()
    {
        private readonly string ConfigFilename;
        private readonly string WorldConfigStringName;

        private ICoreAPI api;

        private T _modConfig;

        public T modConfig
        {
            get
            {
                if (_modConfig == null) { Reload(); }
                return _modConfig;
            }
            set
            {
                _modConfig = value;
            }
        }

        public ConfigManager(ICoreAPI api, string filename)
        {
            ConfigFilename = filename;
            WorldConfigStringName = $"{filename}.config";
            this.api = api;

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
                        api.Logger.Event("[{0}] got world config:-\n{1}", new object[] { ConfigFilename, jsonConfig });

                        _modConfig = JsonConvert.DeserializeObject<T>(jsonConfig);
                    }
                    else
                    {
                        api.Logger.Error("[{0}] failed to acquire world config", new object[] { ConfigFilename });
                        // TODO: implement attempted re-acquisition of world config
                    }
                    break;

                case (EnumAppSide.Server):
                    api.Logger.Event("[{0}] trying to load config", new object[] { ConfigFilename });
                    _modConfig = api.LoadModConfig<T>($"{ConfigFilename}.json");
                    if (_modConfig == null)
                    {
                        api.Logger.Event("[{0}] generating new config", new object[] { ConfigFilename });
                        _modConfig = new();
                        api.StoreModConfig(_modConfig, $"{ConfigFilename}.json");
                    }
                    else api.Logger.Event("[{0}] config loaded", new object[] { ConfigFilename });

                    jsonConfig = JsonConvert.SerializeObject(_modConfig);
                    api.World.Config.SetString(WorldConfigStringName, jsonConfig);
                    api.Logger.Event("[{0}] set world config:-\n{1}", new object[] { ConfigFilename, jsonConfig });

                    break;
            }
        }
    }
}
