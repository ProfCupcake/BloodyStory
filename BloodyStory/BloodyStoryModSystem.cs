using BloodyStory.Config;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Data;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BloodyStory
{
    public class BloodyStoryModSystem : ModSystem
    {
        public const string bloodParticleNetChannel = "bloodystory:particles";
        public const string bleedCheckHotkeyCode = "bleedCheck";
        public BloodyStoryModConfig modConfig => Config.modConfig;

        Harmony harmony;

        public static ConfigManager<BloodyStoryModConfig> Config
        {
            get; private set;
        }

        ICoreAPI api;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

            DefaultEntityBehviors_Patch.api = api;

            harmony = new("bloodystory");
            harmony.PatchAll();
        }

        private void OnEntityLoaded(Entity entity)
        {
            if (entity.GetBehavior<EntityBehaviorHealth>() is not null)
            {
                if (entity.GetBehavior<EntityBehaviorBleed>() is null)
                {
                    EntityBehaviorBleed ebb = new EntityBehaviorBleed(entity);
                    entity.AddBehavior(ebb);
                    ebb.AfterInitialized(false);
                    api.Logger.Event($"[BS-allbleed] Added bleed EB to {entity.GetName()}");
                }
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            api.Event.OnEntityLoaded += OnEntityLoaded;
            api.Event.OnEntitySpawn += OnEntityLoaded;

            Config = new(api, "bloodystory");
            
            api.Network.RegisterUdpChannel(bloodParticleNetChannel)
                .RegisterMessageType<BleedParticles>();

            api.Network.RegisterChannel(bleedCheckHotkeyCode)
                .RegisterMessageType<long>();

            api.RegisterEntityBehaviorClass("bleed", typeof(EntityBehaviorBleed));
            //api.RegisterEntityBehaviorClass("health_bs", typeof(EntityBehaviorHealth_BS));

            api.World.Config.SetFloat("playerHealthRegenSpeed", 0f); // this is probably fine
        }

        public override void Dispose()
        {
            base.Dispose();

            harmony.UnpatchAll("bloodystory");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            sapi.ChatCommands.Create("bleed")
                .WithDescription("Outputs precise bleed and regen levels")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.root)
                .WithArgs(new ICommandArgumentParser[] { sapi.ChatCommands.Parsers.OnlinePlayer("player") })
                .HandleWith(BleedCommand);

            sapi.ChatCommands.Create("makeMeBleed")
                .WithDescription("Adds points of bleeding")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.root)
                .WithArgs(new ICommandArgumentParser[] { sapi.ChatCommands.Parsers.OptionalDouble("bleedAmount", 1) })
                .HandleWith(MakeMeBleedCommand);

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

            sapi.Network.GetUdpChannel(bloodParticleNetChannel)
                    .SetMessageHandler<BleedParticles>(ServerSpawnParticles_Player);

            sapi.Network.GetChannel(bleedCheckHotkeyCode)
                .SetMessageHandler<long>(ServerSendBleedCheck);
        }

        private void ServerSpawnParticles_Player(IServerPlayer fromPlayer, BleedParticles packet)
        {
            sapi.Network.GetUdpChannel(bloodParticleNetChannel).BroadcastPacket(packet, fromPlayer);
        }

        private void ClientSpawnParticles(BleedParticles packet)
        {
            packet.RecalculateBasePos(api);
            if (packet.basePos.SquareDistanceTo(capi.World.Player.Entity.Pos) < 4096)
            {
                api.World.SpawnParticles(packet);
            }
        }

        private TextCommandResult ToggleBleedingCommand(TextCommandCallingArgs args)
        {
            EntityBehaviorBleed bleedEB = args.Caller.Entity.GetBehavior<EntityBehaviorBleed>();

            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (bleedEB.pauseBleedProcess)
            {
                bleedEB.pauseBleedProcess = false;
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("bloodystory:command-bleed-resumed"), EnumChatType.Notification);
            }
            else
            {
                bleedEB.pauseBleedProcess = true;
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("bloodystory:command-bleed-paused"), EnumChatType.Notification);
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
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("bloodystory:command-bleed-resumed"), EnumChatType.Notification);
            }
            else
            {
                bleedEB.pauseBleedParticles = true;
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("bloodystory:command-bleed-paused"), EnumChatType.Notification);
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
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("bloodystory:command-bleedout-enabled"), EnumChatType.Notification);
            }
            else
            {
                dele = (out bool shouldDie, DamageSource _) => { shouldDie = false; };
                bleedEB.OnBleedout += dele;
                bleedoutDelegateDict.Add(bleedEB, dele);
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("bloodystory:command-bleedout-disabled"), EnumChatType.Notification);
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

        private TextCommandResult MakeMeBleedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            double amount = (double)args[0];

            player.Entity.GetBehavior<EntityBehaviorBleed>().bleedLevel += amount;

            return TextCommandResult.Success(Lang.Get("bloodystory:command-bleed-added", new object[] { amount }));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            capi.Input.RegisterHotKey(bleedCheckHotkeyCode, "Check bleeding", GlKeys.B, HotkeyType.CharacterControls); // TODO: localisation

            capi.Input.SetHotKeyHandler(bleedCheckHotkeyCode, bleedCheck);

            capi.Network.GetUdpChannel(bloodParticleNetChannel)
                    .SetMessageHandler<BleedParticles>(ClientSpawnParticles);
        }

        private bool bleedCheck(KeyCombination kc)
        {
            long targetID;
            if (capi.World.Player.CurrentEntitySelection is not null)
            {
                targetID = capi.World.Player.CurrentEntitySelection.Entity.EntityId;
            } else
            {
                targetID = capi.World.Player.Entity.EntityId;
            }

            capi.Network.GetChannel(bleedCheckHotkeyCode).SendPacket<long>(targetID);
            return true;
        }

        private void ServerSendBleedCheck(IServerPlayer fromPlayer, long targetID)
        {
            Entity target = api.World.GetEntityById(targetID);

            if (target is null || target.GetBehavior<EntityBehaviorBleed>() is null)
            {
                target = fromPlayer.Entity;
            }

            EntityBehaviorBleed bleedEB = target.GetBehavior<EntityBehaviorBleed>();

            string message;

            if (modConfig.detailedBleedCheck)
            {
                message = $"{target.GetName()}'s bleeding:-\n" + Lang.Get("bloodystory:command-bleed-stats", new object[] { bleedEB.bleedLevel, bleedEB.GetBleedRate(true), bleedEB.GetRegenRate(true), bleedEB.regenBoost });
            }
            else
            {
                string bleedRating; // TODO: replace this hardcoded placeholder with a proper solution, also localisation

                double bleedLevel = bleedEB.bleedLevel;
                if (bleedLevel <= 0)
                {
                    bleedRating = "None";
                }
                else if (bleedLevel <= modConfig.bleedRating_Trivial)
                {
                    bleedRating = "Trivial";
                }
                else if (bleedLevel <= modConfig.bleedRating_Minor)
                {
                    bleedRating = "Minor";
                }
                else if (bleedLevel <= modConfig.bleedRating_Moderate)
                {
                    bleedRating = "Moderate";
                }
                else if (bleedLevel <= modConfig.bleedRating_Severe)
                {
                    bleedRating = "Severe";
                }
                else
                {
                    bleedRating = "Extreme";
                }

                message = $"{target.GetName()}'s bleeding: {bleedRating}";
            }

            sapi.SendMessage(fromPlayer, GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }

        private TextCommandResult BleedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args[0] as IServerPlayer;
            EntityBehaviorBleed bleedEB = player.Entity.GetBehavior<EntityBehaviorBleed>();

            string message = player.PlayerName + "'s bleeding:-\n"
                + Lang.Get("bloodystory:command-bleed-stats", new object[] { bleedEB.bleedLevel, bleedEB.GetBleedRate(true), bleedEB.GetRegenRate(true), bleedEB.regenBoost });

            return TextCommandResult.Success(message);
        }
    }
}
