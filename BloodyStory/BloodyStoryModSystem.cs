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

        static readonly string bleedAttr = "BS_bleed";
        static readonly string regenAttr = "BS_regen";

        static readonly string sitStartTimeAttr = "BS_sitStartTime";

        static readonly string netChannel = "BS_networkChannel";

        static readonly int tickRate = 1000/15;

        static Dictionary<IServerPlayer, DamageSource> lastHit = new();

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

            api.World.Config.SetFloat("playerHealthRegenSpeed", 0f);
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

        static private void BroadcastConfig()
        {
            NetManager.BroadcastConfig();
        }
        
        private TextCommandResult ReloadConfigCommand(TextCommandCallingArgs args)
        {
            ReloadConfig();

            return TextCommandResult.Success();
        }

        static private void ReloadConfig()
        {
            ConfigManager.Reload();

            BroadcastConfig();
        }

        private TextCommandResult MakeMeBleedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            double amount = (double)args[0];

            player.Entity.WatchedAttributes.SetDouble(bleedAttr, player.Entity.WatchedAttributes.GetDouble(bleedAttr) + amount);

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
            SyncedTreeAttribute playerAttributes = player.Entity.WatchedAttributes;
            
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Bleed level: "+playerAttributes.GetDouble(bleedAttr), EnumChatType.Notification);

            double bleedRate = playerAttributes.GetDouble(bleedAttr);
            bleedRate /= player.Entity.Controls.Sneak ? modConfig.bleedQuotient * modConfig.sneakMultiplier : modConfig.bleedQuotient;
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Current bleed rate: "+bleedRate+" HP/s", EnumChatType.Notification);

            EntityBehaviorHealth pHealth = player.Entity.GetBehavior<EntityBehaviorHealth>();
            EntityBehaviorHunger pHunger = player.Entity.GetBehavior<EntityBehaviorHunger>();
            // TODO: separate regen/bleed rate calculations into methods for deduplication?
            double regenRate = 0;
            if (pHunger.Saturation > 0)
            {
                regenRate = modConfig.baseRegen + modConfig.bonusRegen * (pHealth.MaxHealth - pHealth.BaseMaxHealth);
                if (bleedRate <= 0)
                {
                    regenRate *= player.Entity.MountedOn is not null and BlockEntityBed ? modConfig.regenBedMultiplier : 1;
                    if (player.Entity.Controls.FloorSitting)
                    {
                        long sitStartTime = playerAttributes.GetLong(sitStartTimeAttr);
                        if (sitStartTime + modConfig.regenSitDelay < player.Entity.World.ElapsedMilliseconds)
                        {
                            regenRate *= modConfig.regenSitMultiplier;
                        }
                    }
                }

                regenRate *= Interpolate(modConfig.minSatietyMultiplier, modConfig.maxSatietyMultiplier, pHunger.Saturation / pHunger.MaxSaturation);
            }

            double regenBoost = playerAttributes.GetDouble(regenAttr);
            if (regenBoost > 0)
            {
                regenRate += modConfig.regenBoostRate;
            }
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Current regen rate: " + regenRate + " HP/s", EnumChatType.Notification);
            
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Remaining regen boost: " + regenBoost + " HP", EnumChatType.Notification);

            return TextCommandResult.Success();
        }
    }
}
