using BloodyStory.Config;
using BloodyStory.Lib;
using System;
using System.Net.Sockets;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static BloodyStory.Lib.BloodMath;

namespace BloodyStory
{
    public class EntityBehaviorBleed : EntityBehavior
    {
        static string bloodParticleNetChannel => BloodyStoryModSystem.bloodParticleNetChannel;
        static BloodyStoryModConfig modConfig => BloodyStoryModSystem.Config.modConfig;

        public event OnBleedoutDelegate OnBleedout;

        DamageSource lastHit;

        float hungerTickTimer;
        float hungerConsumption;

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
        public bool pauseBleedProcess
        {
            get => entity.WatchedAttributes.GetBool("BS_pause");
            set => entity.WatchedAttributes.SetBool("BS_pause", value);
        }
        public bool pauseBleedParticles
        {
            get => entity.WatchedAttributes.GetBool("BS_pauseParticles");
            set => entity.WatchedAttributes.SetBool("BS_pauseParticles", value);
        }
        public double regenRate_clientSync
        {
            get => entity.WatchedAttributes.GetDouble("BS_regenRate_clientSync");
            set => entity.WatchedAttributes.SetDouble("BS_regenRate_clientSync", value);
        }

        private long SitStartTime;

        private double t;

        public EntityBehaviorBleed(Entity entity) : base(entity) { }

        public override string PropertyName()
        {
            return "bleed";
        }

        private bool initialised = false;

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            if (entity.World.Side == EnumAppSide.Server)
            {
                EntityBehaviorHealth_BS pHealth = entity.GetBehavior<EntityBehaviorHealth>() as EntityBehaviorHealth_BS;

                if (entity.Api.ModLoader.GetMod("overhaullib") != null)
                {
                    if (entity is EntityPlayer)
                    {
                        COCompat.AddCODamageEH_Player((EntityPlayer)entity, this);
                    } else
                    {
                        COCompat.AddCODamageEH_NPC(entity, this);
                    }
                }
                else
                {
                    pHealth.onDamagedPost += HandleDamage;
                }
            }

            initialised = true;
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity == null || !entity.Alive) return;

            if (modConfig == null) return;

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
                if (pauseBleedParticles) return;
                if (t > modConfig.bloodParticleDelay)
                {
                    SpawnBloodParticles_Player();
                    t = 0;
                }
                else t += dt;
            }
        }

        private void ServerTick(float dtr)
        {
            if (pauseBleedProcess) return;

            if (entity is EntityPlayer)
            {
                IServerPlayer serverPlayer = (IServerPlayer)((EntityPlayer)entity).Player;

                if (serverPlayer.ConnectionState != EnumClientState.Playing) return;
            }

            float dt = dtr * entity.World.Calendar.CalendarSpeedMul * entity.World.Calendar.SpeedOfTime; // realtime -> game time
            dt /= 30; // 24 hrs -> 48 mins
            dt *= modConfig.timeDilation;

            EntityBehaviorHealth pHealth = entity.GetBehavior<EntityBehaviorHealth>();
            EntityBehaviorHunger pHunger = entity.GetBehavior<EntityBehaviorHunger>();

            double bleedDmg = GetBleedRate(true) * modConfig.bleedQuotient; // TODO: maybe fix this hacky "solution"

            double regenRate = GetRegenRate(false);

            if (bleedLevel > 0)
            {
                double dt_peak = (bleedDmg - regenRate * modConfig.bleedQuotient) / modConfig.bleedHealRate;
                if (dt_peak < dt)
                {
                    if (CalculateDmgCum(dt_peak, bleedDmg, regenRate, regenBoost) > pHealth.Health)
                    {
                        BleedOut();
                    }
                }
                bleedLevel = Math.Max(bleedLevel - modConfig.bleedHealRate * dt, 0);
                SitStartTime = 0;
            }

            float beforeHealth = pHealth.Health;
            pHealth.Health = (float)Math.Min(pHealth.Health - CalculateDmgCum(dt, bleedDmg, regenRate, regenBoost), pHealth.MaxHealth); // TODO: handle edge case where bleeding would have stopped within dt given? (probably unnecessary)
            if (pHealth.Health < 0)
            {
                BleedOut();
            }

            if (pHunger != null)
            {
                // Note: regen boost is also included in this now
                // I dunno if that's a good thing or not
                if (beforeHealth < pHealth.MaxHealth || bleedLevel > 0)
                {
                    if (beforeHealth == pHealth.MaxHealth && pHealth.Health == pHealth.MaxHealth)
                    {
                        // bleed rate > 0, but < regen, and is capping at max health
                        // therefore, total health regen equals amount of bleed this tick
                        hungerConsumption += (float)(CalculateDmgCum(dt, bleedDmg, 0) * modConfig.satietyConsumption);
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
                        hungerConsumption += (float)(modConfig.satietyConsumption * regenRate * dt);
                        hungerConsumption += (float)Math.Min(dt * modConfig.regenBoostRate, regenBoost) * modConfig.satietyConsumption;
                    }
                }
                hungerTickTimer += dtr;
                if (hungerTickTimer > 1f)
                    if (hungerConsumption > 0)
                    {
                        pHunger.ConsumeSaturation(hungerConsumption);
                        hungerTickTimer = 0f;
                        hungerConsumption = 0f;
                    }
            }

            if (regenBoost != 0)
            {
                regenBoost -= modConfig.regenBoostRate * dt;
                if (regenBoost < 0) regenBoost = 0;
            }

            if (entity is not EntityPlayer) SpawnBloodParticles_NPC();
        }

        private void BleedOut()
        {
            if (OnBleedout != null)
            {
                bool shouldDie = true;
                foreach (OnBleedoutDelegate d in OnBleedout.GetInvocationList())
                {
                    d(out shouldDie, lastHit);
                }
                if (!shouldDie) return;
            }

            entity.Die(EnumDespawnReason.Death, lastHit);
        }

        public double GetBleedRate(bool includeSneak = true)
        {
            double quot = modConfig.bleedQuotient;
            
            if (entity is EntityPlayer)
            {
                if (includeSneak && ((EntityPlayer)entity).Controls.Sneak) quot *= modConfig.sneakMultiplier;
            }

            return bleedLevel / quot;
        }

        public double GetRegenRate(bool includeBoost = false)
        {
            if (entity is not EntityPlayer) return modConfig.baseRegen;

            if (entity.Api.Side == EnumAppSide.Client)
            {
                if (includeBoost && regenBoost > 0)
                {
                    return regenRate_clientSync + modConfig.regenBoostRate;
                } else
                {
                    return regenRate_clientSync;
                }
            }
            
            EntityBehaviorHealth pHealth = entity.GetBehavior<EntityBehaviorHealth>();
            EntityBehaviorHunger pHunger = entity.GetBehavior<EntityBehaviorHunger>();

            double bleedRate = bleedLevel;
            double regenRate = pHunger.Saturation > 0 ? modConfig.baseRegen + modConfig.bonusRegen * (pHealth.MaxHealth - pHealth.BaseMaxHealth) : 0;

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

            regenRate_clientSync = regenRate; // TODO: fix this awful hack solution
            
            if (includeBoost && regenBoost > 0) regenRate += modConfig.regenBoostRate;

            return regenRate;
        }

        internal float HandleDamage(float damage, DamageSource dmgSource)
        {
            if (dmgSource.Source == EnumDamageSource.Revive) return damage;

            SyncedTreeAttribute playerAttributes = entity.WatchedAttributes;

            if (dmgSource.Source == EnumDamageSource.Void) return damage;

            if (dmgSource.Type != EnumDamageType.Heal)
            {
                regenBoost = 0;
            }

            switch (dmgSource.Type) // possible alternate implementation: dictionary, with dmg type as keys and functions as values?
            {
                case EnumDamageType.Heal: // healing items reduce bleed rate
                    // TODO: add alternative healing method, to allow direct healing?
                    damage *= modConfig.bandageMultiplier;
                    damage *= Math.Max(0, entity.Stats.GetBlended("healingeffectivness"));
                    double bleedRate = bleedLevel;
                    bleedRate -= damage;
                    if (bleedRate < 0) bleedRate = 0;
                    bleedLevel = bleedRate;
                    if (entity is EntityPlayer)
                    {
                        ((IServerPlayer)((EntityPlayer)entity).Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bloodystory:damagelog-bleed-healed", new object[] { Math.Round(damage / modConfig.bleedQuotient, 3) }), EnumChatType.Notification);
                    }
                    ReceiveDamageReplacer(dmgSource, damage);
                    damage = 0;
                    break;
                case EnumDamageType.BluntAttack:
                    ApplyBleed(ref damage, modConfig.bleedDamageMultiplier_blunt, modConfig.directDamageMultiplier_blunt, dmgSource);
                    break;
                case EnumDamageType.SlashingAttack:
                    ApplyBleed(ref damage, modConfig.bleedDamageMultiplier_slash, modConfig.directDamageMultiplier_slash, dmgSource);
                    break;
                case EnumDamageType.PiercingAttack:
                    ApplyBleed(ref damage, modConfig.bleedDamageMultiplier_pierce, modConfig.directDamageMultiplier_pierce, dmgSource);
                    break;
                case EnumDamageType.Poison:
                    ApplyBleed(ref damage, modConfig.bleedDamageMultiplier_poison, modConfig.directDamageMultiplier_poison, dmgSource);
                    break;
                case EnumDamageType.Gravity: break;
                case EnumDamageType.Fire:
                    bleedLevel -= damage * modConfig.bleedCautMultiplier;
                    if (bleedLevel < 0) bleedLevel = 0;
                    if (entity is EntityPlayer)
                    {
                        ((IServerPlayer)((EntityPlayer)entity).Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bloodystory:damagelog-bleed-cauterised", new object[] { Math.Round(damage * modConfig.bleedCautMultiplier / modConfig.bleedQuotient, 3) }), EnumChatType.Notification);
                    }
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

        internal void ApplyBleed(ref float damage, float bleedDamageMult, float directDamageMult, DamageSource dmgSource)
        {
            float bleedDamage = damage;
            bleedDamage *= bleedDamageMult;
            bleedLevel += bleedDamage;

            lastHit = dmgSource;

            if (entity is EntityPlayer)
            {
                ((IServerPlayer)((EntityPlayer)entity).Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bloodystory:damagelog-bleed-gained", new object[] { Math.Round(bleedDamage / modConfig.bleedQuotient, 3) }), EnumChatType.Notification);
            }
            ReceiveDamageReplacer(dmgSource, bleedDamage);

            damage *= directDamageMult;
        }

        // Handles knockback, hurt animation, etc.
        // Required as game will not do these if received damage is reduced to zero
        // TODO: replace this with a more elegant solution, if one exists
        internal void ReceiveDamageReplacer(DamageSource dmgSource, float damage)
        {
            SyncedTreeAttribute playerAttributes = entity.WatchedAttributes;

            // from EntityBehaviorHealth.OnEntityReceiveDamage
            if (entity.Alive)
            {
                entity.OnHurt(dmgSource, damage);
                if (damage > 1f) entity.AnimManager.StartAnimation("hurt");
                if (dmgSource.Type != EnumDamageType.Heal) entity.PlayEntitySound("hurt", null, true, 24f);
            }

            // from Entity.ReceiveDamage
            if (dmgSource.Type != EnumDamageType.Heal && damage > 0f)
            {
                playerAttributes.SetInt("onHurtCounter", playerAttributes.GetInt("onHurtCounter") + 1);
                playerAttributes.SetFloat("onHurt", damage);
                if (damage > 0.05f)
                {
                    entity.AnimManager.StartAnimation("hurt");
                }

                if (dmgSource.GetSourcePosition() != null)
                {
                    bool verticalAttack = false;
                    double attackYaw;
                    double attackPitch;
                    if (dmgSource.GetAttackAngle(entity.Pos.XYZ, out attackYaw, out attackPitch))
                    {
                        verticalAttack = Math.Abs(attackPitch) > 1.3962633609771729 || Math.Abs(attackPitch) < 0.1745329201221466;
                    }
                    Vec3d dir = (entity.SidedPos.XYZ - dmgSource.GetSourcePosition()).Normalize();
                    if (verticalAttack)
                    {
                        dir.Y = 0.05000000074505806;
                        dir.Normalize();
                    }
                    else
                    {
                        dir.Y = 0.699999988079071;
                    }
                    dir.Y /= (double)dmgSource.YDirKnockbackDiv;
                    float factor = dmgSource.KnockbackStrength * GameMath.Clamp((1f - entity.Properties.KnockbackResistance) / 10f, 0f, 1f);
                    entity.WatchedAttributes.SetFloat("onHurtDir", (float)Math.Atan2(dir.X, dir.Z));
                    entity.WatchedAttributes.SetDouble("kbdirX", dir.X * (double)factor);
                    entity.WatchedAttributes.SetDouble("kbdirY", dir.Y * (double)factor);
                    entity.WatchedAttributes.SetDouble("kbdirZ", dir.Z * (double)factor);
                }
                else
                {
                    entity.WatchedAttributes.SetDouble("kbdirX", 0.0);
                    entity.WatchedAttributes.SetDouble("kbdirY", 0.0);
                    entity.WatchedAttributes.SetDouble("kbdirZ", 0.0);
                    entity.WatchedAttributes.SetFloat("onHurtDir", -999f);
                }
            }
        }

        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10, float nutritionGainMultiplier = 1)
        {
            double regenBoostAdd = saturation / modConfig.regenBoostQuotient;
            regenBoost += regenBoostAdd;
            if (entity is EntityPlayer)
            {
                ((IServerPlayer)((EntityPlayer)entity).Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bloodystory:damagelog-regenboost-gained", new object[] { Math.Round(regenBoostAdd, 1) }), EnumChatType.Notification);
            }
        }

        void SpawnBloodParticles_Player()
        {
            double bleedAmount = bleedLevel;
            bleedAmount *= modConfig.bloodParticleMultiplier;

            if (entity is EntityPlayer)
            {
                EntityPlayer playerEntity = entity as EntityPlayer;
                bleedAmount /= playerEntity.Controls.Sneak ? modConfig.sneakMultiplier : 1;
            }
            double bloodHeight = entity.LocalEyePos.Y / 2;

            float yaw = entity.Pos.Yaw;
            yaw -= (float)(Math.PI / 2); // for some reason, in 1.20, player yaw is now rotated by a quarter turn?

            float posOffset_x = (float)(0.2f * Math.Cos(yaw + Math.PI / 2));
            float posOffset_y = (float)(-0.2f * Math.Sin(yaw + Math.PI / 2));
            BleedParticles bloodParticleProperties = new();
            bloodParticleProperties.Quantity = NatFloat.createUniform((float)bleedAmount, (float)bleedAmount * 0.75f);
            bloodParticleProperties.basePos = entity.Pos.XYZ.Add(-0.2f * Math.Cos(yaw + Math.PI / 2), bloodHeight, 0.2f * Math.Sin(yaw + Math.PI / 2));
            bloodParticleProperties.PosOffset = new NatFloat[]
            {
                NatFloat.createUniform(posOffset_x, posOffset_x),
                NatFloat.createUniform(0.2f,0.2f),
                NatFloat.createUniform(posOffset_y, posOffset_y)
            };
            bloodParticleProperties.Velocity = new NatFloat[]
            {
                NatFloat.createUniform((float)(1.05f * Math.Cos(yaw) + entity.Pos.Motion.X), 0.35f * (float)Math.Cos(yaw)),
                NatFloat.createUniform(0.175f + (float)entity.Pos.Motion.Y, 0.5025f),
                NatFloat.createUniform((float)(-1.05f * Math.Sin(yaw) + entity.Pos.Motion.Z), -0.35f * (float)Math.Sin(yaw))
            };

            entity.Api.Logger.Event("[BS-particles] client sending particles packet");
            ((ICoreClientAPI)entity.Api).Network.GetUdpChannel(bloodParticleNetChannel).SendPacket(bloodParticleProperties);
        }
        
        void SpawnBloodParticles_NPC()
        {
            double bleedAmount = bleedLevel;
            bleedAmount *= modConfig.bloodParticleMultiplier;

            if (entity is EntityPlayer)
            {
                EntityPlayer playerEntity = entity as EntityPlayer;
                bleedAmount /= playerEntity.Controls.Sneak ? modConfig.sneakMultiplier : 1;
            }
            double bloodHeight = entity.LocalEyePos.Y / 2;

            float yaw = entity.Pos.Yaw;
            yaw -= (float)(Math.PI / 2); 

            float posOffset_x = (float)(0.2f * Math.Cos(yaw + Math.PI / 2));
            float posOffset_y = (float)(-0.2f * Math.Sin(yaw + Math.PI / 2));
            BleedParticles bloodParticleProperties = new();
            bloodParticleProperties.Quantity = NatFloat.createUniform((float)bleedAmount, (float)bleedAmount * 0.75f);
            bloodParticleProperties.basePos = entity.Pos.XYZ.Add(-0.2f * Math.Cos(yaw + Math.PI / 2), bloodHeight, 0.2f * Math.Sin(yaw + Math.PI / 2));
            bloodParticleProperties.PosOffset = new NatFloat[]
            {
                NatFloat.createUniform(posOffset_x, posOffset_x),
                NatFloat.createUniform(0.2f,0.2f),
                NatFloat.createUniform(posOffset_y, posOffset_y)
            };
            bloodParticleProperties.Velocity = new NatFloat[]
            {
                NatFloat.createUniform((float)(1.05f * Math.Cos(yaw) + entity.Pos.Motion.X), 0.35f * (float)Math.Cos(yaw)),
                NatFloat.createUniform(0.175f + (float)entity.Pos.Motion.Y, 0.5025f),
                NatFloat.createUniform((float)(-1.05f * Math.Sin(yaw) + entity.Pos.Motion.Z), -0.35f * (float)Math.Sin(yaw))
            };

            entity.Api.Logger.Event("[BS-particles] server broadcasting NPC particles packet");
            ((ICoreServerAPI)entity.Api).Network.GetUdpChannel(bloodParticleNetChannel).BroadcastPacket(bloodParticleProperties);
        }
    }
}
