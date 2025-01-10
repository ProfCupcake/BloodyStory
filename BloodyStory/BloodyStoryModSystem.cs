using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using HarmonyLib;
using System.Collections.Generic;
using ProtoBuf;
using static BloodyStory.BloodMath;

namespace BloodyStory
{
    public class BloodyStoryModSystem : ModSystem // rewrite all of this as an entitybehaviour at some point
    {
        public static BloodyStoryModConfig modConfig
        {
            get
            {
                return ConfigManager.modConfig;
            }
        }

        static ICoreAPI api;
        static ICoreClientAPI capi;
        static ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            BloodyStoryModSystem.api = api;
            NetManager.Initialise(api);
            ConfigManager.Initialise(api);

            api.RegisterEntityBehaviorClass("bleed", typeof(EntityBehaviorBleed));

            api.World.Config.SetFloat("playerHealthRegenSpeed", 0f); // this is probably fine
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            sapi.ChatCommands.Create("bleed")
                .WithDescription("Outputs precise bleed and regen levels")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(BleedCommand);
            
            sapi.ChatCommands.Create("makeMeBleed")
                .WithDescription("Adds points of bleeding")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.root)
                .WithArgs(new ICommandArgumentParser[] { sapi.ChatCommands.Parsers.OptionalDouble("bleedAmount", 1) })
                .HandleWith(MakeMeBleedCommand);

            sapi.ChatCommands.Create("bsconfigreload")
                .WithDescription("Reloads Bloody Story config file")
                .RequiresPrivilege(Privilege.root)
                .HandleWith(ReloadConfigCommand);
        }
        
        private TextCommandResult ReloadConfigCommand(TextCommandCallingArgs args)
        {
            ConfigManager.Reload();

            return TextCommandResult.Success();
        }
        private TextCommandResult MakeMeBleedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            double amount = (double)args[0];

            player.Entity.GetBehavior<EntityBehaviorBleed>().bleedLevel += amount;

            player.SendMessage(GlobalConstants.GeneralChatGroup, "Added " + amount + " bleed", EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
        }
        private TextCommandResult BleedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            EntityBehaviorBleed bleedEB = player.Entity.GetBehavior<EntityBehaviorBleed>();
            
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleed level: " + bleedEB.bleedLevel, EnumChatType.Notification);

            player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleed rate: " + bleedEB.GetBleedRate(true) + " HP/s", EnumChatType.Notification);
            
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Current regen rate: " + bleedEB.GetRegenRate(true) + " HP/s", EnumChatType.Notification);
            
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Remaining regen boost: " + bleedEB.regenBoost + " HP", EnumChatType.Notification);

            return TextCommandResult.Success();
        }
    }
}
