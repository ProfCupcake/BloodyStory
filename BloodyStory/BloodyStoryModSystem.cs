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

namespace BloodyStory
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class BloodyStoryModConfig // TODO: proper config documentation
    {
        public double baseRegen = 0.02f; // hp regen per second
        public double bonusRegen = 0.0016f; // added regen per point of additional max health; max bonus is this * 12.5

        public double regenBoostRate = 0.5f; // additional hp regen per second for the duration of the food boost
        public double regenBoostQuotient = 100f; // quotient for the amount of hp added per point of satiety increase for regen boost

        public double regenBedMultiplier = 8f; // multiplier for regen when lying in bed

        public int regenSitDelay = 5000; // delay before the sit boost is applied, in ms
        public double regenSitMultiplier = 3f; // multiplier for regen when sitting

        public double bleedHealRate = 0.15f; // natural bleeding reduction
        public double bleedQuotient = 12f; // quotient for hp loss to bleed
        public double sneakMultiplier = 8f; // multiplier for bleed quotient applied when sneaking

        public double bleedCautMultiplier = 1f; // multiplier for how much bleed is reduced by fire damage

        public double bloodParticleMultiplier = 1f; // multiplier for the quantity of blood particles produced

        public float bandageMultiplier = 1f; // multiplier for the amount of bleed reduction when using bandages/poultice

        public float maxSatietyMultiplier = 1.2f; // multiplier for regen rate at maximum hunger saturation
        public float minSatietyMultiplier = 0f; // multiplier for regen rate at minimum hunger saturation

        public float satietyConsumption = 1f; // hunger saturation consumed per point of hp restored (sans bonus)

        public float timeDilation = 1.0f; // to adjust simulated second speed, for if game speed is changed
    }

    [ProtoContract]
    public class NetMessage_Request
    {
    }

    [HarmonyPatch]
    public class BloodyStoryModSystem : ModSystem // rewrite all of this as an entitybehaviour at some point? (probably a separate mod)
    {
        static BloodyStoryModConfig modConfig;

        Harmony harmony;

        static readonly string bleedAttr = "BS_bleed";
        static readonly string regenAttr = "BS_regen";

        static readonly string sitStartTimeAttr = "BS_sitStartTime";

        static readonly string netChannel = "BS_networkChannel";

        static readonly int tickRate = 1000/15;

        static Dictionary<IServerPlayer, DamageSource> lastHit = new();

        double lastUpdate = -1;


        static ICoreAPI api;
        static ICoreClientAPI capi;
        static ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            BloodyStoryModSystem.api = api;

            api.World.Config.SetFloat("playerHealthRegenSpeed", 0f);

            harmony = new("com.profcupcake.bloodystory");
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony.UnpatchAll("com.profcupcake.bloodystory");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            sapi.Network.RegisterChannel(netChannel)
                .RegisterMessageType(typeof(NetMessage_Request))
                .RegisterMessageType(typeof(BloodyStoryModConfig))
                .SetMessageHandler<NetMessage_Request>(Net_HandleRequest);

            ReloadConfig();
            
            sapi.Event.PlayerNowPlaying += OnPlayerJoined;
            sapi.Event.PlayerRespawn += OnPlayerRespawn;
            sapi.Event.RegisterGameTickListener(Tick, tickRate);

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

            harmony.PatchAll();
        }
        
        static private void Net_HandleRequest(IServerPlayer player, NetMessage_Request request)
        {
            SendConfig(player);
        }

        static private void SendConfig(IServerPlayer player)
        {
            sapi.Network.GetChannel(netChannel)
                .SendPacket<BloodyStoryModConfig>(modConfig, player);
        }

        static private void BroadcastConfig()
        {
            sapi.Network.GetChannel(netChannel)
                .BroadcastPacket<BloodyStoryModConfig>(modConfig);
        }
        
        private TextCommandResult ReloadConfigCommand(TextCommandCallingArgs args)
        {
            ReloadConfig();

            return TextCommandResult.Success();
        }

        static private void ReloadConfig()
        {
            modConfig = api.LoadModConfig<BloodyStoryModConfig>("BloodyStory.json");
            if (modConfig == null)
            {
                modConfig = new BloodyStoryModConfig();
                api.StoreModConfig(modConfig, "BloodyStory.json");
            }

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

            TryRegisterClientNetChannel();

            RequestConfig();

            IClientPlayer player = capi.World.Player;

            capi.Event.RegisterGameTickListener((float dt) => ClientTick(dt, player), tickRate);
        }
        static private void TryRegisterClientNetChannel()
        {
            capi.Network.RegisterChannel(netChannel)
                .RegisterMessageType(typeof(NetMessage_Request))
                .RegisterMessageType(typeof(BloodyStoryModConfig))
                .SetMessageHandler<BloodyStoryModConfig>(Net_HandleSend);
        }

        static private void Net_HandleSend(BloodyStoryModConfig send)
        {
            modConfig = send;
        }

        static private void RequestConfig()
        {
            if (capi.Network.GetChannelState(netChannel) == EnumChannelState.Connected)
            {
                capi.Network.GetChannel(netChannel).SendPacket<NetMessage_Request>(new());
            } else
            {
                TryRegisterClientNetChannel();
            }
        }

        private void OnPlayerJoined(IServerPlayer byPlayer)
        {
            EntityBehaviorHealth pHealth = byPlayer.Entity.GetBehavior<EntityBehaviorHealth>();

            //pHealth._playerHealthRegenSpeed = 0; // this is probably ok

            pHealth.onDamaged += ((float dmg, DamageSource dmgSrc) => HandleDamage(byPlayer, dmg, dmgSrc));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityBehaviorHunger), nameof(EntityBehaviorHunger.OnEntityReceiveSaturation))]
        private static void HandleEating(EntityBehaviorHunger __instance, float saturation/*, EnumFoodCategory foodCat, float saturationLossDelay, float nutritionGainMultiplier*/)
        {
            IServerPlayer player = (IServerPlayer)((EntityPlayer)__instance.entity).Player;
            if (player != null)
            {
                double regenBoostAdd = saturation / modConfig.regenBoostQuotient;
                player.Entity.WatchedAttributes.SetDouble(regenAttr, player.Entity.WatchedAttributes.GetDouble(regenAttr) + regenBoostAdd);
                player.SendMessage(GlobalConstants.DamageLogChatGroup, "Received ~" + Math.Round(regenBoostAdd, 1) + " HP of regen boost from food", EnumChatType.Notification); //TODO: localisation
            }
        }
        private void OnPlayerRespawn(IServerPlayer byPlayer)
        {
            byPlayer.Entity.WatchedAttributes.SetDouble(bleedAttr, 0);
            byPlayer.Entity.WatchedAttributes.SetDouble(regenAttr, 0);
        }

        private static float HandleDamage(IServerPlayer byPlayer, float damage, DamageSource dmgSource)
        {
            if (dmgSource.Source == EnumDamageSource.Revive) return damage;

            SyncedTreeAttribute playerAttributes = byPlayer.Entity.WatchedAttributes;

            if (dmgSource.Type != EnumDamageType.Heal)
            {
                playerAttributes.SetDouble(regenAttr, 0);
            }

            if (dmgSource.Source == EnumDamageSource.Void) return damage;

            switch (dmgSource.Type) // possible alternate implementation: dictionary, with dmg type as keys and functions as values?
            {
                case EnumDamageType.Heal: // healing items reduce bleed rate
                    // TODO: add alternative healing method, to allow direct healing?
                    damage *= modConfig.bandageMultiplier;
                    damage *= Math.Max(0, byPlayer.Entity.Stats.GetBlended("healingeffectivness"));
                    double bleedRate = playerAttributes.GetDouble(bleedAttr);
                    bleedRate -= damage;
                    if (bleedRate < 0) bleedRate = 0;
                    playerAttributes.SetDouble(bleedAttr, bleedRate);
                    byPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, "Healed ~" + Math.Round(damage / modConfig.bleedQuotient, 3) + " HP/s bleed", EnumChatType.Notification); // TODO: localisation
                    ReceiveDamageReplacer(byPlayer, dmgSource, damage);
                    damage = 0;
                    break; 
                case EnumDamageType.BluntAttack:
                case EnumDamageType.SlashingAttack:
                case EnumDamageType.PiercingAttack:
                    playerAttributes.SetDouble(bleedAttr, playerAttributes.GetDouble(bleedAttr) + damage);
                    RecordLastHit(byPlayer, dmgSource);
                    byPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, "Received ~" + Math.Round(damage/modConfig.bleedQuotient, 3) + " HP/s bleed", EnumChatType.Notification); // TODO: localisation
                    ReceiveDamageReplacer(byPlayer, dmgSource, damage);
                    damage = 0;
                    break;
                case EnumDamageType.Poison: 
                    playerAttributes.SetDouble(bleedAttr, playerAttributes.GetDouble(bleedAttr) + damage);
                    RecordLastHit(byPlayer, dmgSource);
                    ReceiveDamageReplacer(byPlayer, dmgSource, damage);
                    damage = 0;
                    break;
                case EnumDamageType.Gravity: break;
                case EnumDamageType.Fire:
                    playerAttributes.SetDouble(bleedAttr, playerAttributes.GetDouble(bleedAttr) - (damage * modConfig.bleedCautMultiplier));
                    byPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, "Cauterised ~" + Math.Round(damage * modConfig.bleedCautMultiplier / modConfig.bleedQuotient, 3) + " HP/s bleed", EnumChatType.Notification); // TODO: localisation
                    break; // :]
                case EnumDamageType.Suffocation: break;
                case EnumDamageType.Hunger: break;
                case EnumDamageType.Crushing: break;
                case EnumDamageType.Frost: break;
                case EnumDamageType.Electricity: break;
                case EnumDamageType.Heat: break;
                case EnumDamageType.Injury: break;
                default: break;
            }

            return damage;
        }

        // Handles knockback, hurt animation, etc.
        // Required as game will not do these if received damage is reduced to zero
        // TODO: replace this with a more elegant solution, if one exists (transpile out the health change in onEntityReceiveDamage?)
        public static void ReceiveDamageReplacer(IServerPlayer player, DamageSource dmgSource, float damage)
        {
            SyncedTreeAttribute playerAttributes = player.Entity.WatchedAttributes;

            // from EntityBehaviorHealth.OnEntityReceiveDamage
            if (player.Entity.Alive) 
            {
                player.Entity.OnHurt(dmgSource, damage);
                if (damage > 1f) player.Entity.AnimManager.StartAnimation("hurt");
                player.Entity.PlayEntitySound("hurt", null, true, 24f);
            }

            // from Entity.ReceiveDamage
            if (dmgSource.Type != EnumDamageType.Heal && damage > 0f)
            {
                playerAttributes.SetInt("onHurtCounter", playerAttributes.GetInt("onHurtCounter") + 1);
                playerAttributes.SetFloat("onHurt", damage);
                if (damage > 0.05f)
                {
                    player.Entity.AnimManager.StartAnimation("hurt");
                }

                if (dmgSource.GetSourcePosition() != null)
                {
                    Vec3d dir = (player.Entity.SidedPos.XYZ - dmgSource.GetSourcePosition().Normalize());
                    dir.Y = 0.699999988079071;
                    float factor = dmgSource.KnockbackStrength * GameMath.Clamp((1f - player.Entity.Properties.KnockbackResistance) / 10f, 0f, 1f);
                    playerAttributes.SetFloat("onHurtDir", (float)Math.Atan2(dir.X, dir.Z));
                    playerAttributes.SetDouble("kbdirX", dir.X * (double)factor);
                    playerAttributes.SetDouble("kbdirY", dir.Y * (double)factor);
                    playerAttributes.SetDouble("kbdirZ", dir.Z * (double)factor);
                } else
                {
                    playerAttributes.SetDouble("kbdirX", 0);
                    playerAttributes.SetDouble("kbdirY", 0);
                    playerAttributes.SetDouble("kbdirZ", 0);
                    playerAttributes.SetFloat("onHurtDir", -999f);
                }
            }
        }

        private static void RecordLastHit(IServerPlayer byPlayer, DamageSource dmgSource)
        {
            SyncedTreeAttribute playerAttributes = byPlayer.Entity.WatchedAttributes;

            lastHit[byPlayer] = dmgSource;
        }

        private void ClientTick(float dt, IClientPlayer player)
        {
            if (player == null)
            {
                player = capi.World.Player;
                if (player == null) return;
            }
            if (player.Entity.WatchedAttributes.GetDouble(bleedAttr) > 0f)
            {
                SpawnBloodParticles(player);
            }
        }

        private void Tick(float dt)
        {
            dt *= sapi.World.Calendar.CalendarSpeedMul * sapi.World.Calendar.SpeedOfTime; // realtime -> game time
            dt /= 30; // 24 hrs -> 48 mins
            dt *= modConfig.timeDilation;

            IServerPlayer[] players = (IServerPlayer[])sapi.World.AllOnlinePlayers;
            foreach (IServerPlayer player in players)
            {
                if (player == null || player.ConnectionState != EnumClientState.Playing || !player.Entity.Alive) continue;

                SyncedTreeAttribute playerAttributes = player.Entity.WatchedAttributes;
                EntityBehaviorHealth pHealth = player.Entity.GetBehavior<EntityBehaviorHealth>();
                EntityBehaviorHunger pHunger = player.Entity.GetBehavior<EntityBehaviorHunger>();
                double bleedRate = playerAttributes.GetDouble(bleedAttr);
                double bleedDmg = bleedRate / (player.Entity.Controls.Sneak ? modConfig.sneakMultiplier : 1);

                double regenRate = (pHunger.Saturation > 0) ? modConfig.baseRegen + (modConfig.bonusRegen * (pHealth.MaxHealth - pHealth.BaseMaxHealth)) : 0;
                if (bleedRate <= 0)
                {
                    if (player.Entity.MountedOn is not null and BlockEntityBed)
                    {
                        regenRate *= modConfig.regenBedMultiplier;
                    };
                    if (player.Entity.Controls.FloorSitting)
                    {
                        long sitStartTime = playerAttributes.GetLong(sitStartTimeAttr);
                        if (sitStartTime == 0)
                        {
                            sitStartTime = player.Entity.World.ElapsedMilliseconds;
                            playerAttributes.SetLong(sitStartTimeAttr, sitStartTime);
                        }
                        if (sitStartTime + modConfig.regenSitDelay < player.Entity.World.ElapsedMilliseconds)
                        {
                            regenRate *= modConfig.regenSitMultiplier;
                        }
                    } else playerAttributes.SetLong(sitStartTimeAttr, 0);
                }
                regenRate *= Interpolate(modConfig.minSatietyMultiplier, modConfig.maxSatietyMultiplier, pHunger.Saturation / pHunger.MaxSaturation);

                double regenBoost = playerAttributes.GetDouble(regenAttr);

                if (bleedRate > 0)
                {
                    //SpawnBloodParticles(player);

                    double dt_peak = (bleedDmg - (regenRate * modConfig.bleedQuotient)) / modConfig.bleedHealRate;
                    if (dt_peak < dt)
                    {
                        if (CalculateDmgCum(dt_peak, bleedDmg, regenRate, regenBoost) > pHealth.Health)
                        {
                            DamageSource dmgSource;
                            player.Entity.Die(EnumDespawnReason.Death, lastHit.TryGetValue(player, out dmgSource) ? dmgSource : null);
                            continue;
                        }
                    }
                    bleedRate = Math.Max(bleedRate - modConfig.bleedHealRate * dt, 0);

                    playerAttributes.SetDouble(bleedAttr, bleedRate);
                    playerAttributes.SetLong(sitStartTimeAttr, 0);
                }

                float beforeHealth = pHealth.Health;
                pHealth.Health = (float)Math.Min(pHealth.Health - CalculateDmgCum(dt, bleedDmg, regenRate, regenBoost), pHealth.MaxHealth); // TODO: handle edge case where bleeding would have stopped within dt given? (probably unnecessary)
                if (pHealth.Health < 0)
                {
                    DamageSource dmgSource;
                    player.Entity.Die(EnumDespawnReason.Death, lastHit.TryGetValue(player, out dmgSource) ? dmgSource : null);
                    continue;
                }

                // Note: regen boost is also included in this now
                // I dunno if that's a good thing or not
                float hungerConsumption = 0;
                if (beforeHealth < pHealth.MaxHealth || bleedRate > 0)
                {
                    if (beforeHealth == pHealth.MaxHealth && pHealth.Health == pHealth.MaxHealth)
                    {
                        // bleed rate > 0, but < regen, and is capping at max health
                        // therefore, total health regen equals amount of bleed this tick
                        hungerConsumption = (float)(CalculateDmgCum(dt, bleedDmg, 0) * modConfig.satietyConsumption);
                    }
                    else/* if (pHealth.Health == pHealth.MaxHealth)
                    {
                        // we managed to regen to full health at some point this tick
                        // figure out when we hit full health, then how much regen happened in that time
                        // (possibly ignore this edge case?)
                        // ... yes, I am going to ignore this edge case because the formula is stupid long lmao
                    } else //*/
                    {
                        // simplest situation: we have been doing health regen this whole tick, so just calculate total health regen
                        hungerConsumption = (float)(modConfig.satietyConsumption * regenRate * dt);
                        hungerConsumption += (float)Math.Min(dt * modConfig.regenBoostRate, regenBoost) * modConfig.satietyConsumption;
                    }
                }
                pHunger.ConsumeSaturation(hungerConsumption);

                if (regenBoost != 0)
                {
                    regenBoost -= modConfig.regenBoostRate * dt;
                    if (regenBoost < 0) regenBoost = 0;
                    playerAttributes.SetDouble(regenAttr, regenBoost);
                }
                

            }
            lastUpdate = sapi.World.Calendar.ElapsedHours;
        }

        static double CalculateDmgCum(double dt, double bleedDmg, double regenRate, double regenBoost = 0)
        {
            double num = 2 * bleedDmg * dt - modConfig.bleedHealRate * Math.Pow(dt, 2) - 2 * modConfig.bleedQuotient * regenRate * dt;
            double den = 2 * modConfig.bleedQuotient;
            return (num / den) - Math.Min(dt*modConfig.regenBoostRate, regenBoost);
        }

        private static float Interpolate(float min, float max, float w, float p = 1)
        {
            return (min + (max - min) * (float)Math.Pow(w,p));
        }

        private static void SpawnBloodParticles(IClientPlayer player)
        {
            if (modConfig == null)
            {
                RequestConfig();
                return;
            }

            double bleedAmount = player.Entity.WatchedAttributes.GetDouble(bleedAttr);
            bleedAmount /= (player.Entity.Controls.Sneak ? modConfig.sneakMultiplier : 1);
            bleedAmount *= modConfig.bloodParticleMultiplier;
            double bloodHeight = player.Entity.LocalEyePos.Y/2;
            if (player.Entity.Controls.FloorSitting) bloodHeight /= 4;
            else if (player.Entity.Controls.Sneak) bloodHeight /= 2;

            float playerYaw = player.Entity.Pos.Yaw;
            playerYaw -= (float)(Math.PI / 2); // for some reason, in 1.20, player yaw is now rotated by a quarter turn?

            AdvancedParticleProperties[] waterBloodParticleProperties = new AdvancedParticleProperties[]
            {
                new()
                {
                    Quantity = NatFloat.One,
                    ParentVelocityWeight = 1f,
                    Velocity = new NatFloat[]
                    {
                        NatFloat.Zero,
                        NatFloat.Zero,
                        NatFloat.Zero
                    },
                    HsvaColor = new NatFloat[]
                    {
                        NatFloat.Zero,
                        NatFloat.createUniform(255f,0f),
                        NatFloat.createUniform(255f,0f),
                        NatFloat.createUniform(255f,0f)
                    },
                    PosOffset = new NatFloat[]
                    {
                        NatFloat.Zero,
                        NatFloat.Zero,
                        NatFloat.Zero
                    },
                    ParticleModel = EnumParticleModel.Quad,
                    DieInAir = true,
                    SwimOnLiquid = false,
                    GravityEffect = NatFloat.createUniform(0.05f, 0.05f),
                    Size = NatFloat.createUniform(0.2f, 0.15f),
                    SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARINCREASE, 1f),
                    OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -255f)
                }
            };

            float posOffset_x = (float)(0.2f * Math.Cos(playerYaw + (Math.PI / 2)));
            float posOffset_y = (float)(-0.2f * Math.Sin(playerYaw + (Math.PI / 2)));

            AdvancedParticleProperties bloodParticleProperties = new AdvancedParticleProperties()
            {
                Quantity = NatFloat.createUniform((float)bleedAmount, (float)bleedAmount * 0.75f),
                HsvaColor = new NatFloat[]
                {
                    NatFloat.Zero,
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(255f,0f)
                },
                basePos = player.Entity.Pos.XYZ.Add(-0.2f * Math.Cos(playerYaw + (Math.PI / 2)), bloodHeight, 0.2f * Math.Sin(playerYaw + (Math.PI / 2))),
                PosOffset = new NatFloat[]
                {
                    NatFloat.createUniform(posOffset_x, posOffset_x),
                    NatFloat.createUniform(0.2f,0.2f),
                    NatFloat.createUniform(posOffset_y, posOffset_y)
                },
                LifeLength = NatFloat.One,
                GravityEffect = NatFloat.One,
                Size = NatFloat.createUniform(0.35f, 0.15f),
                DieInLiquid = true,
                Velocity = new NatFloat[]
                {
                    NatFloat.createUniform((float)((1.05f * Math.Cos(playerYaw)) + player.Entity.Pos.Motion.X), 0.35f * (float)Math.Cos(playerYaw)),
                    NatFloat.createUniform(0.175f + (float)player.Entity.Pos.Motion.Y, 0.5025f),
                    NatFloat.createUniform((float)((-1.05f * Math.Sin(playerYaw)) + player.Entity.Pos.Motion.Z), -0.35f * (float)Math.Sin(playerYaw))
                },
                DeathParticles = waterBloodParticleProperties,
                ParticleModel = EnumParticleModel.Cube
            };

            player.Entity.World.SpawnParticles(bloodParticleProperties);
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
