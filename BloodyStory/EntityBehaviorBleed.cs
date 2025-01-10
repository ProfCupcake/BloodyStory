using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static BloodyStory.BloodMath;
using CombatOverhaul;
using CombatOverhaul.DamageSystems;
using System.Runtime.Loader;

namespace BloodyStory
{
    internal class EntityBehaviorBleed : EntityBehavior
    {
        static BloodyStoryModConfig modConfig => ConfigManager.modConfig;

        DamageSource lastHit;

        public double bleedLevel
        {
            get => entity.WatchedAttributes.GetDouble("BS_bleed");
            set => entity.WatchedAttributes.SetDouble("BS_bleed", value);
        }
        public double regenBoost
        {
            get => entity.WatchedAttributes.GetDouble("BS_regen");
            set => entity.WatchedAttributes.SetDouble("BS_regen", value);
        }

        private long SitStartTime;

        private double t;

        public EntityBehaviorBleed(Entity entity) : base(entity) {}

        public override string PropertyName()
        {
            return "bleed";
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            if (entity.World.Side == EnumAppSide.Server)
            {
                IServerPlayer player = ((EntityPlayer)entity).Player as IServerPlayer;
                EntityBehaviorHealth pHealth = entity.GetBehavior<EntityBehaviorHealth>();

                if (entity.Api.ModLoader.GetMod("combatoverhaul") != null)
                {
                    PlayerDamageModelBehavior pDamageModel = entity.GetBehavior<PlayerDamageModelBehavior>();
                    pDamageModel.OnReceiveDamage += (ref float dmg, DamageSource dmgSrc, PlayerBodyPart bodyPart) => { dmg = HandleDamage(player, dmg, dmgSrc); };
                    pHealth.onDamaged += (float dmg, DamageSource dmgSrc) =>
                    {
                        if (dmgSrc.Type == EnumDamageType.BluntAttack
                            || dmgSrc.Type == EnumDamageType.SlashingAttack
                            || dmgSrc.Type == EnumDamageType.PiercingAttack) return dmg;
                        return HandleDamage(player, dmg, dmgSrc); // bit jank, might change later, idk
                    };
                } else
                {
                    pHealth.onDamaged += (float dmg, DamageSource dmgSrc) => HandleDamage(player, dmg, dmgSrc);
                }
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            switch (entity.World.Side)
            {
                case EnumAppSide.Server:
                    ServerTick(deltaTime);
                    break;
                case EnumAppSide.Client:
                    ClientTick(deltaTime);
                    break;
            }
        }

        private void ClientTick(float dt)
        {
            if (bleedLevel > 0f)
            {
                if (t > modConfig.bloodParticleDelay)
                {
                    SpawnBloodParticles();
                    t = 0;
                }
                else t += dt;
            }
        }

        private void ServerTick(float dt)
        {
            if (entity == null || !entity.Alive || entity.WatchedAttributes.GetBool("unconscious")) return;

            if (((EntityPlayer)entity).Player is not IServerPlayer serverPlayer || serverPlayer.ConnectionState != EnumClientState.Playing) return;

            if (modConfig == null) return;

            dt *= entity.World.Calendar.CalendarSpeedMul * entity.World.Calendar.SpeedOfTime; // realtime -> game time
            dt /= 30; // 24 hrs -> 48 mins
            dt *= modConfig.timeDilation;

            EntityBehaviorHealth pHealth = entity.GetBehavior<EntityBehaviorHealth>();
            EntityBehaviorHunger pHunger = entity.GetBehavior<EntityBehaviorHunger>();

            double bleedDmg = GetBleedRate(true) * modConfig.bleedQuotient; // TODO: maybe fix this hacky "solution"

            double regenRate = GetRegenRate(false);

            if (bleedLevel > 0)
            {
                double dt_peak = (bleedDmg - (regenRate * modConfig.bleedQuotient)) / modConfig.bleedHealRate;
                if (dt_peak < dt)
                {
                    if (CalculateDmgCum(dt_peak, bleedDmg, regenRate, regenBoost) > pHealth.Health)
                    {
                        entity.Die(EnumDespawnReason.Death, lastHit);
                    }
                }
                bleedLevel = Math.Max(bleedLevel - modConfig.bleedHealRate * dt, 0);
                SitStartTime = 0;
            }

            float beforeHealth = pHealth.Health;
            pHealth.Health = (float)Math.Min(pHealth.Health - CalculateDmgCum(dt, bleedDmg, regenRate, regenBoost), pHealth.MaxHealth); // TODO: handle edge case where bleeding would have stopped within dt given? (probably unnecessary)
            if (pHealth.Health < 0)
            {
                entity.Die(EnumDespawnReason.Death, lastHit);
            }

            // Note: regen boost is also included in this now
            // I dunno if that's a good thing or not
            float hungerConsumption = 0;
            if (beforeHealth < pHealth.MaxHealth || bleedLevel > 0)
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
            }
        }
        
        public double GetBleedRate(bool includeSneak = true)
        {
            double quot = modConfig.bleedQuotient;
            if (includeSneak && ((EntityPlayer)entity).Controls.Sneak) quot *= modConfig.sneakMultiplier;
            return bleedLevel / quot;
        }

        public double GetRegenRate(bool includeBoost = false)
        {
            EntityBehaviorHealth pHealth = entity.GetBehavior<EntityBehaviorHealth>();
            EntityBehaviorHunger pHunger = entity.GetBehavior<EntityBehaviorHunger>();
            
            double bleedRate = bleedLevel;
            double regenRate = (pHunger.Saturation > 0) ? modConfig.baseRegen + (modConfig.bonusRegen * (pHealth.MaxHealth - pHealth.BaseMaxHealth)) : 0;
            if (bleedRate <= 0)
            {
                if (((EntityPlayer)entity).MountedOn is not null and BlockEntityBed)
                {
                    regenRate *= modConfig.regenBedMultiplier;
                };
                if (((EntityPlayer)entity).Controls.FloorSitting)
                {
                    if (SitStartTime == 0)
                    {
                        SitStartTime = entity.World.ElapsedMilliseconds;
                    }
                    if (SitStartTime + modConfig.regenSitDelay < entity.World.ElapsedMilliseconds)
                    {
                        regenRate *= modConfig.regenSitMultiplier;
                    }
                }
                else SitStartTime = 0;
            }
            regenRate *= Interpolate(modConfig.minSatietyMultiplier, modConfig.maxSatietyMultiplier, pHunger.Saturation / pHunger.MaxSaturation);

            if (includeBoost && regenBoost > 0) regenRate += modConfig.regenBoostRate;

            return regenRate;
        }

        private float HandleDamage(IServerPlayer byPlayer, float damage, DamageSource dmgSource)
        {
            if (dmgSource.Source == EnumDamageSource.Revive) return damage;

            SyncedTreeAttribute playerAttributes = byPlayer.Entity.WatchedAttributes;

            if (dmgSource.Type != EnumDamageType.Heal)
            {
                regenBoost = 0;
            }

            if (dmgSource.Source == EnumDamageSource.Void) return damage;

            if (playerAttributes.GetBool("unconscious")) return damage;

            switch (dmgSource.Type) // possible alternate implementation: dictionary, with dmg type as keys and functions as values?
            {
                case EnumDamageType.Heal: // healing items reduce bleed rate
                    // TODO: add alternative healing method, to allow direct healing?
                    damage *= modConfig.bandageMultiplier;
                    damage *= Math.Max(0, byPlayer.Entity.Stats.GetBlended("healingeffectivness"));
                    double bleedRate = bleedLevel;
                    bleedRate -= damage;
                    if (bleedRate < 0) bleedRate = 0;
                    bleedLevel = bleedRate;
                    byPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, "Healed ~" + Math.Round(damage / modConfig.bleedQuotient, 3) + " HP/s bleed", EnumChatType.Notification); // TODO: localisation
                    ReceiveDamageReplacer(byPlayer, dmgSource, damage);
                    damage = 0;
                    break;
                case EnumDamageType.BluntAttack:
                case EnumDamageType.SlashingAttack:
                case EnumDamageType.PiercingAttack:
                    bleedLevel += damage;
                    lastHit = dmgSource;
                    byPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, "Received ~" + Math.Round(damage / modConfig.bleedQuotient, 3) + " HP/s bleed", EnumChatType.Notification); // TODO: localisation
                    ReceiveDamageReplacer(byPlayer, dmgSource, damage);
                    damage = 0;
                    break;
                case EnumDamageType.Poison:
                    bleedLevel += damage;
                    lastHit = dmgSource;
                    ReceiveDamageReplacer(byPlayer, dmgSource, damage);
                    damage = 0;
                    break;
                case EnumDamageType.Gravity: break;
                case EnumDamageType.Fire:
                    bleedLevel -= damage * modConfig.bleedCautMultiplier; 
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
                }
                else
                {
                    playerAttributes.SetDouble("kbdirX", 0);
                    playerAttributes.SetDouble("kbdirY", 0);
                    playerAttributes.SetDouble("kbdirZ", 0);
                    playerAttributes.SetFloat("onHurtDir", -999f);
                }
            }
        }

        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10, float nutritionGainMultiplier = 1)
        {
            double regenBoostAdd = saturation / modConfig.regenBoostQuotient;
            regenBoost += regenBoostAdd;
            ((IServerPlayer)((EntityPlayer)entity).Player).SendMessage(GlobalConstants.DamageLogChatGroup, "Received ~" + Math.Round(regenBoostAdd, 1) + " HP of regen boost from food", EnumChatType.Notification); //TODO: localisation
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            regenBoost = 0;
            bleedLevel = 0;
        }

        private static readonly AdvancedParticleProperties[] waterBloodParticleProperties = new AdvancedParticleProperties[]
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

        private static readonly AdvancedParticleProperties bloodParticleProperties = new()
        {
            HsvaColor = new NatFloat[]
                {
                    NatFloat.Zero,
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(255f,0f)
                },
            LifeLength = NatFloat.One,
            GravityEffect = NatFloat.One,
            Size = NatFloat.createUniform(0.35f, 0.15f),
            DieInLiquid = true,
            DeathParticles = waterBloodParticleProperties,
            ParticleModel = EnumParticleModel.Cube
        };

        void SpawnBloodParticles()
        {
            if (modConfig == null) return;

            EntityPlayer playerEntity = entity as EntityPlayer;

            double bleedAmount = bleedLevel;
            bleedAmount /= (playerEntity.Controls.Sneak ? modConfig.sneakMultiplier : 1);
            bleedAmount *= modConfig.bloodParticleMultiplier;
            double bloodHeight = playerEntity.LocalEyePos.Y / 2;

            float playerYaw = playerEntity.Pos.Yaw;
            playerYaw -= (float)(Math.PI / 2); // for some reason, in 1.20, player yaw is now rotated by a quarter turn?

            float posOffset_x = (float)(0.2f * Math.Cos(playerYaw + (Math.PI / 2)));
            float posOffset_y = (float)(-0.2f * Math.Sin(playerYaw + (Math.PI / 2)));

            bloodParticleProperties.Quantity = NatFloat.createUniform((float)bleedAmount, (float)bleedAmount * 0.75f);

            bloodParticleProperties.basePos = playerEntity.Pos.XYZ.Add(-0.2f * Math.Cos(playerYaw + (Math.PI / 2)), bloodHeight, 0.2f * Math.Sin(playerYaw + (Math.PI / 2)));
            bloodParticleProperties.PosOffset = new NatFloat[]
            {
                NatFloat.createUniform(posOffset_x, posOffset_x),
                NatFloat.createUniform(0.2f,0.2f),
                NatFloat.createUniform(posOffset_y, posOffset_y)
            };

            bloodParticleProperties.Velocity = new NatFloat[]
            {
                NatFloat.createUniform((float)((1.05f * Math.Cos(playerYaw)) + playerEntity.Pos.Motion.X), 0.35f * (float)Math.Cos(playerYaw)),
                NatFloat.createUniform(0.175f + (float)playerEntity.Pos.Motion.Y, 0.5025f),
                NatFloat.createUniform((float)((-1.05f * Math.Sin(playerYaw)) + playerEntity.Pos.Motion.Z), -0.35f * (float)Math.Sin(playerYaw))
            };

            playerEntity.World.SpawnParticles(bloodParticleProperties);
        }
    }
}
