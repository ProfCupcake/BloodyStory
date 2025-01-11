using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BloodyStory
{
    internal class ConfigManager
    {
        private static readonly string configFilename = "bloodystory.json";

        internal static bool receivedConfig = false;

        private static ICoreAPI api;
        private static BloodyStoryModConfig _modConfig;
        public static BloodyStoryModConfig modConfig
        {
            get
            {
                if (_modConfig == null) { Reload(); }
                else if (api.Side == EnumAppSide.Client)
                {
                    if (!receivedConfig) Reload();
                }
                return _modConfig;
            }
            set
            {
                _modConfig = value;
            }
        }

        public static void Initialise(ICoreAPI api)
        {
            ConfigManager.api = api;
        }

        internal static void Reload()
        {
            switch (api.Side)
            {
                case (EnumAppSide.Server):
                    _modConfig = api.LoadModConfig<BloodyStoryModConfig>(configFilename);
                    if (_modConfig == null)
                    {
                        _modConfig = new BloodyStoryModConfig();
                        api.StoreModConfig(_modConfig, configFilename);
                    }
                    NetManager.BroadcastConfig();
                    break;
                case (EnumAppSide.Client):
                    _modConfig = new BloodyStoryModConfig();
                    NetManager.RequestConfig();
                    break;
            }
        }
    }
}
