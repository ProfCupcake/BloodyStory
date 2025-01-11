using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

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

            sapi.ChatCommands.Create("preventbleedout")
                .WithDescription("Toggles player bleedout (player will not die from bleed)")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.root)
                .HandleWith(ToggleBleedoutCommand);

            sapi.ChatCommands.Create("togglebleeding")
                .WithDescription("Toggle processing of player bleeding")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.root)
                .HandleWith(ToggleBleedingCommand);

            sapi.ChatCommands.Create("togglebleedparticles")
                .WithDescription("Toggles spawning bleed particles")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.root)
                .HandleWith(ToggleBleedParticlesCommand);

            sapi.Event.PlayerRespawn += OnPlayerRespawn;
        }

        private TextCommandResult ToggleBleedingCommand(TextCommandCallingArgs args)
        {
            EntityBehaviorBleed bleedEB = args.Caller.Entity.GetBehavior<EntityBehaviorBleed>();

            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (bleedEB.pauseBleedProcess)
            {
                bleedEB.pauseBleedProcess = false;
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleeding resumed", EnumChatType.Notification);
            }
            else
            {
                bleedEB.pauseBleedProcess = true;
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleeding paused", EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult ToggleBleedParticlesCommand(TextCommandCallingArgs args)
        {
            EntityBehaviorBleed bleedEB = args.Caller.Entity.GetBehavior<EntityBehaviorBleed>();

            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (bleedEB.pauseBleedParticles)
            {
                bleedEB.pauseBleedParticles = false;
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleeding resumed", EnumChatType.Notification);
            }
            else
            {
                bleedEB.pauseBleedParticles = true;
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleeding paused", EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }

        Dictionary<EntityBehaviorBleed, OnBleedoutDelegate> bleedoutDelegateDict;

        private TextCommandResult ToggleBleedoutCommand(TextCommandCallingArgs args)
        {
            if (bleedoutDelegateDict == null)
            {
                bleedoutDelegateDict = new();
            }
            EntityPlayer plEnt = args.Caller.Entity as EntityPlayer;
            IServerPlayer player = plEnt.Player as IServerPlayer;
            EntityBehaviorBleed bleedEB = plEnt.GetBehavior<EntityBehaviorBleed>();

            if (bleedoutDelegateDict.TryGetValue(bleedEB, out OnBleedoutDelegate dele))
            {
                bleedEB.OnBleedout -= dele;
                bleedoutDelegateDict.Remove(bleedEB);
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleedout enabled", EnumChatType.Notification);
            }
            else
            {
                dele = (out bool shouldDie, DamageSource _) => { shouldDie = false; };
                bleedEB.OnBleedout += dele;
                bleedoutDelegateDict.Add(bleedEB, dele);
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleedout disabled", EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }

        private void OnPlayerRespawn(IServerPlayer byPlayer)
        {
            EntityBehaviorBleed bleedEB = byPlayer.Entity.GetBehavior<EntityBehaviorBleed>();

            bleedEB.bleedLevel = 0;
            bleedEB.regenBoost = 0;
            bleedEB.pauseBleedParticles = false;
            bleedEB.pauseBleedProcess = false;
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
