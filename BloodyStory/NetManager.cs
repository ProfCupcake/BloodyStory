using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BloodyStory
{
    internal class NetManager
    {
        private static INetworkAPI napi;
        private static IServerNetworkAPI snapi;
        private static IClientNetworkAPI cnapi;
        private static readonly string netChannel = "bloodystory";
        public static void Initialise(ICoreAPI api)
        {
            napi = api.Network;
            napi.RegisterChannel(netChannel)
                .RegisterMessageType<BloodyStoryModConfig>()
                .RegisterMessageType<NetMessage_Request>();
            
            switch (api.Side)
            {
                case (EnumAppSide.Server):
                    snapi = (IServerNetworkAPI)napi;
                    snapi.GetChannel(netChannel).SetMessageHandler<NetMessage_Request>(SendConfig);
                    break;
                case (EnumAppSide.Client):
                    cnapi = (IClientNetworkAPI)napi;
                    cnapi.GetChannel(netChannel).SetMessageHandler<BloodyStoryModConfig>(ReceiveConfig);
                    break;
            }
        }
        internal static void RequestConfig()
        {
            IClientNetworkChannel channel = cnapi.GetChannel(netChannel);
            if (channel.Connected) channel.SendPacket(new NetMessage_Request());
        }
        private static void SendConfig(IServerPlayer fromPlayer, NetMessage_Request packet)
        {
            snapi.GetChannel(netChannel).SendPacket(ConfigManager.modConfig, fromPlayer);
        }
        private static void ReceiveConfig(BloodyStoryModConfig packet)
        {
            ConfigManager.modConfig = packet;
        }
        internal static void BroadcastConfig()
        {
            snapi.GetChannel(netChannel).BroadcastPacket(ConfigManager.modConfig);
        }
    }

    [ProtoContract]
    public class NetMessage_Request {}
}
