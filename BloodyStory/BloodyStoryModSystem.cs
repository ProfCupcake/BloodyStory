﻿using BloodyStory.Config;
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
        public static BloodyStoryModSystem instance;
        
        public const string bleedCheckHotkeyCode = "bleedCheck";
        public BloodyStoryModConfig modConfig => Config.modConfig;

        Harmony harmony;

        public static ConfigManager<BloodyStoryModConfig> Config
        {
            get; private set;
        }

        static ICoreAPI api;
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
            if (entity is null) return;
            if (modConfig.allShallBleed || entity is EntityPlayer)
            {
                if (entity.GetBehavior<EntityBehaviorHealth>() is not null || entity.WatchedAttributes.GetBool("BS_hasBleedEB"))
                {
                    if (entity.GetBehavior<EntityBehaviorBleed>() is null)
                    {
                        EntityBehaviorBleed ebb = new EntityBehaviorBleed(entity);
                        entity.AddBehavior(ebb);
                        ebb.AfterInitialized(false);
                    }
                }
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            BloodyStoryModSystem.api = api;

            api.Event.OnEntityLoaded += OnEntityLoaded;
            api.Event.OnEntitySpawn += OnEntityLoaded;

            Config = new(api, "bloodystory");

            api.RegisterEntityBehaviorClass("bleed", typeof(EntityBehaviorBleed));

            api.World.Config.SetFloat("playerHealthRegenSpeed", 0f); // this is probably fine
        }

        public override void Dispose()
        {
            base.Dispose();

            EntityBehaviorBleed.ClearEntityConfigDict();

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

            capi.Event.RegisterGameTickListener(ClientCheckLoadedEntities, 5000, 5000);
        }

        private void ClientCheckLoadedEntities(float obj)
        {
            foreach (Entity entity in capi.World.LoadedEntities.Values)
            {
                OnEntityLoaded(entity);
            }
        }

        private bool bleedCheck(KeyCombination kc)
        {
            Entity target = capi.World.Player.CurrentEntitySelection?.Entity;
            
            if (target is null || target.GetBehavior<EntityBehaviorBleed>() is null)
            {
                target = capi.World.Player.Entity;
            }

            string message;

            if (target.Alive)
            {

                EntityBehaviorBleed bleedEB = target.GetBehavior<EntityBehaviorBleed>();

                if (modConfig.detailedBleedCheck)
                {
                    message = Lang.Get("bloodystory:bleedrating-prefix", new object[] { target.GetName() }) + ":- \n" + Lang.Get("bloodystory:command-bleed-stats", new object[] { bleedEB.bleedLevel, bleedEB.GetBleedRate(true), bleedEB.GetRegenRate(true), bleedEB.regenBoost });
                }
                else
                {
                    string bleedRating; // TODO: replace this hardcoded placeholder with a proper solution

                    double bleedLevel = bleedEB.bleedLevel;
                    if (bleedLevel <= 0)
                    {
                        bleedRating = "bloodystory:bleedrating-none";
                    }
                    else if (bleedLevel <= modConfig.bleedRating_Trivial)
                    {
                        bleedRating = "bloodystory:bleedrating-trivial";
                    }
                    else if (bleedLevel <= modConfig.bleedRating_Minor)
                    {
                        bleedRating = "bloodystory:bleedrating-minor";
                    }
                    else if (bleedLevel <= modConfig.bleedRating_Moderate)
                    {
                        bleedRating = "bloodystory:bleedrating-moderate";
                    }
                    else if (bleedLevel <= modConfig.bleedRating_Severe)
                    {
                        bleedRating = "bloodystory:bleedrating-severe";
                    }
                    else
                    {
                        bleedRating = "bloodystory:bleedrating-extreme";
                    }

                    message = $"{Lang.Get("bloodystory:bleedrating-prefix", new object[] { target.GetName() })}: {Lang.Get(bleedRating)}";
                }
            } else
            {
                message = Lang.Get("bloodystory:bleedrating-target-dead", new object[] { target.GetName() });
            }

            capi.ShowChatMessage(message);

            return true;
        }

        private TextCommandResult BleedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args[0] as IServerPlayer;
            EntityBehaviorBleed bleedEB = player.Entity.GetBehavior<EntityBehaviorBleed>();

            string message = Lang.Get("bloodystory:bleedrating-prefix", new object[] {player.PlayerName}) + ":-\n"
                + Lang.Get("bloodystory:command-bleed-stats", new object[] { bleedEB.bleedLevel, bleedEB.GetBleedRate(true), bleedEB.GetRegenRate(true), bleedEB.regenBoost });

            return TextCommandResult.Success(message);
        }
    }
}
