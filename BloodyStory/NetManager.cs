using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BloodyStory
{
    internal class NetManager
    {
        private static ICoreAPI api;
        private static IServerNetworkAPI snapi;
        private static IClientNetworkAPI cnapi;
        private static readonly string netChannel = "bloodystory";
        public static void Initialise(ICoreAPI api)
        {
            NetManager.api = api;
            api.Network.RegisterChannel(netChannel)
                .RegisterMessageType<BloodyStoryModConfig>()
                .RegisterMessageType<NetMessage_Request>();

            switch (api.Side)
            {
                case (EnumAppSide.Server):
                    snapi = (IServerNetworkAPI)api.Network;
                    snapi.GetChannel(netChannel).SetMessageHandler<NetMessage_Request>(SendConfig);
                    break;
                case (EnumAppSide.Client):
                    cnapi = (IClientNetworkAPI)api.Network;
                    cnapi.GetChannel(netChannel).SetMessageHandler<BloodyStoryModConfig>(ReceiveConfig);
                    break;
            }
        }
        internal static void RequestConfig()
        {
            IClientNetworkChannel channel = cnapi.GetChannel(netChannel);
            if (channel.Connected)
            {
                channel.SendPacket(new NetMessage_Request());
            }
            else
            {
                api.Event.RegisterCallback(RequestConfig, 100); 
            }
        }
        private static void RequestConfig(float obj)
        {
            RequestConfig();
        }

        private static void SendConfig(IServerPlayer fromPlayer, NetMessage_Request packet)
        {
            snapi.GetChannel(netChannel).SendPacket(ConfigManager.modConfig, fromPlayer);
        }
        private static void ReceiveConfig(BloodyStoryModConfig packet)
        {
            ConfigManager.modConfig = packet;
            ConfigManager.receivedConfig = true;
            api.Logger.Event("[bloodystory] Received config from server");
        }
        internal static void BroadcastConfig()
        {
            snapi.GetChannel(netChannel).BroadcastPacket(ConfigManager.modConfig);
        }
    }

    [ProtoContract]
    public class NetMessage_Request { }
}
