using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static BloodyStory.BloodMath;

namespace BloodyStory
{
    internal class EntityBehaviorBleed : EntityBehavior
    {
        BloodyStoryModConfig modConfig => ConfigManager.modConfig;

        DamageSource lastHit;

        double bleedLevel
        {
            get => entity.WatchedAttributes.GetDouble("BS_bleed");
            set => entity.WatchedAttributes.SetDouble("BS_bleed", value);
        }
        double regenBoost
        {
            get => entity.WatchedAttributes.GetDouble("BS_regen");
            set => entity.WatchedAttributes.SetDouble("BS_regen", value);
        }

        long SitStartTime;

        double t;

        public EntityBehaviorBleed(Entity entity) : base(entity) {}

        public override string PropertyName()
        {
            return "bleed";
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

            IServerPlayer serverPlayer = ((EntityPlayer)entity).Player as IServerPlayer;

            if (serverPlayer.ConnectionState != EnumClientState.Playing) return;

            if (modConfig == null) return;

            dt *= entity.Api.World.Calendar.CalendarSpeedMul * entity.Api.World.Calendar.SpeedOfTime; // realtime -> game time
            dt /= 30; // 24 hrs -> 48 mins
            dt *= modConfig.timeDilation;

            EntityBehaviorHealth pHealth = entity.GetBehavior<EntityBehaviorHealth>();
            EntityBehaviorHunger pHunger = entity.GetBehavior<EntityBehaviorHunger>();
            double bleedRate = bleedLevel;
            double bleedDmg = bleedRate / (serverPlayer.Entity.Controls.Sneak ? modConfig.sneakMultiplier : 1);

            double regenRate = (pHunger.Saturation > 0) ? modConfig.baseRegen + (modConfig.bonusRegen * (pHealth.MaxHealth - pHealth.BaseMaxHealth)) : 0;
            if (bleedRate <= 0)
            {
                if (serverPlayer.Entity.MountedOn is not null and BlockEntityBed)
                {
                    regenRate *= modConfig.regenBedMultiplier;
                };
                if (serverPlayer.Entity.Controls.FloorSitting)
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

            if (bleedRate > 0)
            {
                double dt_peak = (bleedDmg - (regenRate * modConfig.bleedQuotient)) / modConfig.bleedHealRate;
                if (dt_peak < dt)
                {
                    if (CalculateDmgCum(dt_peak, bleedDmg, regenRate, regenBoost) > pHealth.Health)
                    {
                        entity.Die(EnumDespawnReason.Death, lastHit);
                    }
                }
                bleedRate = Math.Max(bleedRate - modConfig.bleedHealRate * dt, 0);

                bleedLevel = bleedRate;
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
            }
        }

        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10, float nutritionGainMultiplier = 1)
        {
            
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
