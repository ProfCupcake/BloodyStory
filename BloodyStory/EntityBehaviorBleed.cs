using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BloodyStory
{
    internal class EntityBehaviorBleed : EntityBehavior
    {
        BloodyStoryModConfig modConfig
        {
            get
            {
                return BloodyStoryModSystem.modConfig;// TODO: actually fix modconfig stuff
            }
        } 
        DamageSource lastHit;
        double bleedLevel
        {
            get
            {
                return entity.WatchedAttributes.GetDouble("BS_bleed");
            }
            set
            {
                entity.WatchedAttributes.SetDouble("BS_bleed", value);
            }
        }
        double regenBoost
        {
            get
            {
                return entity.WatchedAttributes.GetDouble("BS_regen");
            }
            set
            {
                entity.WatchedAttributes.SetDouble("BS_regen", value);
            }
        }
        double SitStartTime;

        double t;

        public EntityBehaviorBleed(Entity entity) : base(entity) {}

        public override string PropertyName()
        {
            return "bleed";
        }

        public override void OnGameTick(float deltaTime)
        {
            switch (entity.Api.Side)
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
                SpawnBloodParticles();
            }
        }

        private void ServerTick(float dt)
        {
            if (t > 2)
            {
                entity.Api.Logger.Debug("nothing to see here");
                t = 0;
            } else
            {
                t += dt;
            }
        }

        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10, float nutritionGainMultiplier = 1)
        {
            
        }

        static AdvancedParticleProperties bloodParticleProperties = new AdvancedParticleProperties()
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

        static AdvancedParticleProperties[] waterBloodParticleProperties = new AdvancedParticleProperties[]
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
