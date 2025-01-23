using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BloodyStory.Config
{
    public class ConfigManager
    {
        private readonly string ConfigFilename;
        private readonly string NetChannel;

        private bool receivedConfig = false;
        private bool requestedConfig = false;

        private ICoreAPI api;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;

        private BloodyStoryModConfig _modConfig;

        public BloodyStoryModConfig modConfig
        {
            get
            {
                if (_modConfig == null) { Reload(); }
                else if (api.Side == EnumAppSide.Client)
                {
                    if (!receivedConfig && !requestedConfig) Reload();
                }
                return _modConfig;
            }
            set
            {
                _modConfig = value;
            }
        }

        public ConfigManager(ICoreAPI api, string filename, string netchannel)
        {
            ConfigFilename = filename;
            NetChannel = netchannel;

            this.api = api;
            switch (api.Side)
            {
                case EnumAppSide.Client:
                    capi = api as ICoreClientAPI;
                    break;
                case EnumAppSide.Server:
                    sapi = api as ICoreServerAPI;
                    break;
            }

            api.Network.RegisterChannel(NetChannel)
                .RegisterMessageType<NetMessage_Request>()
                .RegisterMessageType<BloodyStoryModConfig>();

            switch (api.World.Side)
            {
                case (EnumAppSide.Client):
                    capi.Network.GetChannel(NetChannel).SetMessageHandler<BloodyStoryModConfig>(ReceiveConfig);
                    break;
                case (EnumAppSide.Server):
                    sapi.Network.GetChannel(NetChannel).SetMessageHandler<NetMessage_Request>(SendConfig);
                    Reload();
                    break;
            }
        }

        public void Reload()
        {
            switch (api.Side)
            {
                case (EnumAppSide.Client):
                    _modConfig = new BloodyStoryModConfig();
                    RequestConfig();
                    requestedConfig = true;
                    capi.Event.EnqueueMainThreadTask(() => { capi.Event.RegisterCallback((float d) => { requestedConfig = false; }, 5000); }, "registerconfigcallback");
                    break;
                case (EnumAppSide.Server):
                    api.Logger.Event("[{0}] trying to load config", new object[] {NetChannel});
                    _modConfig = api.LoadModConfig<BloodyStoryModConfig>(ConfigFilename);
                    if (_modConfig == null)
                    {
                        api.Logger.Event("[{0}] generating new config", new object[] { NetChannel });
                        _modConfig = new BloodyStoryModConfig();
                        api.StoreModConfig(_modConfig, ConfigFilename);
                    } else api.Logger.Event("[{0}] config loaded", new object[] { NetChannel });
                    BroadcastConfig();
                    break;
            }
        }
        public void RequestConfig()
        {
            api.Logger.Event("[{0}] requesting config from server", new object[] { NetChannel });
            capi.Network.GetChannel(NetChannel).SendPacket<NetMessage_Request>(new());
        }
        private void ReceiveConfig(BloodyStoryModConfig packet)
        {
            _modConfig = packet;
            receivedConfig = true;
            api.Logger.Event("[{0}] received mod config from server", new object[] { NetChannel });
        }
        private void SendConfig(IServerPlayer fromPlayer, NetMessage_Request packet)
        {
            api.Logger.Event("[{0}] sending mod config to client {1}", new object[] { NetChannel, fromPlayer.PlayerName });
            sapi.Network.GetChannel(NetChannel).SendPacket(modConfig, fromPlayer);
        }
        public void BroadcastConfig()
        {
            api.Logger.Event("[{0}] broadcasting config to all players", new object[] { NetChannel });
            sapi.Network.GetChannel(NetChannel).BroadcastPacket(modConfig);
        }

    }
    [ProtoContract]
    internal class NetMessage_Request { }
}
