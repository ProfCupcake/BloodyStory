using ProtoBuf;
using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace BloodyStory
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    internal class BleedParticles : AsyncAdvancedParticleProperties
    {
        public long entityID;

        public BleedParticles(long entityID, ICoreAPI api)
        {
            this.entityID = entityID;
            RecalculateBasePos(api);
            SetDefaults();
        }

        public BleedParticles()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            HsvaColor = new NatFloat[]
                {
                    NatFloat.createUniform(2f, 0f),
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(125f,0f),
                    NatFloat.createUniform(255f,0f)
                };
            LifeLength = NatFloat.createUniform(30f, 10f);
            GravityEffect = NatFloat.One;
            Size = NatFloat.createUniform(0.35f, 0.15f);
            DieInLiquid = true;
            DeathParticles = new AsyncAdvancedParticleProperties[]{
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
                        NatFloat.createUniform(2f, 0f),
                        NatFloat.createUniform(255f,0f),
                        NatFloat.createUniform(125f,0f),
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
            ParticleModel = EnumParticleModel.Cube;
        }

        public void RecalculateBasePos(ICoreAPI api)
        {
            Entity entity = api.World.GetEntityById(entityID);

            if (entity is not null)
            {
                float yaw = entity.SidedPos.Yaw - (float)(Math.PI / 2);

                basePos = entity.SidedPos.XYZ.Add(
                    -0.2f * Math.Cos(yaw + Math.PI / 2),
                    entity.LocalEyePos.Y / 2,
                    0.2f * Math.Sin(yaw + Math.PI / 2));
            }
        }
    }

    // Literally a copy of AdvancedParticleProperties, but with the async flag no longer locked to false
    public class AsyncAdvancedParticleProperties : IParticlePropertiesProvider
    {
        public bool Async { get; set; }

        public float ParentVelocityWeight { get; set; }

        public bool DieInLiquid { get; set; }

        public bool SwimOnLiquid { get; set; }

        public float Bounciness { get; set; }

        public bool DieInAir { get; set; }

        public bool DieOnRainHeightmap { get; set; }

        public NatFloat Quantity { get; set; }

        float IParticlePropertiesProvider.Quantity
        {
            get
            {
                return Quantity.nextFloat();
            }
        }

        public Vec3d basePos = new();

        private Vec3d tmpPos;

        public NatFloat[] PosOffset;

        private Vec3f tmpVelo;

        public Vec3f baseVelocity = new();

        public Vec3d Pos
        {
            get
            {
                tmpPos.Set(
                    basePos.X + PosOffset[0].nextFloat(),
                    basePos.Y + PosOffset[1].nextFloat(),
                    basePos.Z + PosOffset[2].nextFloat());

                return tmpPos;
            }
        }

        public Vec3f ParentVelocity { get; set; }

        public int LightEmission
        {
            get
            {
                return 0;
            }
        }

        public EvolvingNatFloat OpacityEvolve { get; set; }

        public EvolvingNatFloat RedEvolve { get; set; }

        public EvolvingNatFloat GreenEvolve { get; set; }

        public EvolvingNatFloat BlueEvolve { get; set; }

        public EnumParticleModel ParticleModel { get; set; } = EnumParticleModel.Cube;

        public NatFloat Size;

        float IParticlePropertiesProvider.Size
        {
            get
            {
                return Size.nextFloat();
            }
        }

        public EvolvingNatFloat SizeEvolve { get; set; }

        public EvolvingNatFloat[] VelocityEvolve { get; set; }

        public NatFloat GravityEffect;

        float IParticlePropertiesProvider.GravityEffect
        {
            get
            {
                return GravityEffect.nextFloat();
            }
        }

        public NatFloat LifeLength;

        float IParticlePropertiesProvider.LifeLength
        {
            get
            {
                return LifeLength.nextFloat();
            }
        }

        public int VertexFlags { get; set; }

        public bool SelfPropelled { get; set; }

        public bool TerrainCollision { get; set; }

        public NatFloat SecondarySpawnInterval;

        float IParticlePropertiesProvider.SecondarySpawnInterval
        {
            get
            {
                return SecondarySpawnInterval.nextFloat();
            }
        }

        public IParticlePropertiesProvider[] SecondaryParticles { get; set; }

        public IParticlePropertiesProvider[] DeathParticles { get; set; }

        public bool RandomVelocityChange { get; set; }

        public NatFloat[] HsvaColor;

        public int Color;

        public float WindAffectedness;

        public float WindAffectednessAtPos;

        public NatFloat[] Velocity;

        public bool ColorByBlock;

        public void BeginParticle()
        {
            if (WindAffectedness > 0f)
            {
                ParentVelocityWeight = WindAffectednessAtPos * WindAffectedness;
                ParentVelocity = GlobalConstants.CurrentWindSpeedClient;
            }
        }

        public AsyncAdvancedParticleProperties()
        {
            SecondarySpawnInterval = NatFloat.createUniform(0f, 0f);
            HsvaColor = new NatFloat[]
            {
                NatFloat.createUniform(128f, 128f),
                NatFloat.createUniform(128f, 128f),
                NatFloat.createUniform(128f, 128f),
                NatFloat.createUniform(255f, 0f)
            };
            GravityEffect = NatFloat.createUniform(1f, 0f);
            LifeLength = NatFloat.createUniform(1f, 0f);
            PosOffset = new NatFloat[]
            {
                NatFloat.createUniform(0f,0f),
                NatFloat.createUniform(0f,0f),
                NatFloat.createUniform(0f,0f)
            };
            Quantity = NatFloat.createUniform(1f, 0f);
            Size = NatFloat.createUniform(1f, 0f);
            SizeEvolve = EvolvingNatFloat.createIdentical(0f);
            Velocity = new NatFloat[]
            {
                NatFloat.createUniform(0f, 0.5f),
                NatFloat.createUniform(0f, 0.5f),
                NatFloat.createUniform(0f, 0.5f)
            };
            ParticleModel = EnumParticleModel.Cube;
            TerrainCollision = true;
            basePos = new();
            baseVelocity = new();
            tmpPos = new();
            tmpVelo = new();
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            basePos = new Vec3d(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            DieInAir = reader.ReadBoolean();
            DieInLiquid = reader.ReadBoolean();
            SwimOnLiquid = reader.ReadBoolean();
            HsvaColor = new NatFloat[]
            {
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader)
            };
            GravityEffect = NatFloat.createFromBytes(reader);
            LifeLength = NatFloat.createFromBytes(reader);
            PosOffset = new NatFloat[]
            {
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader)
            };
                Quantity = NatFloat.createFromBytes(reader);
            Size = NatFloat.createFromBytes(reader);
            Velocity = new NatFloat[]
            {
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader),
                NatFloat.createFromBytes(reader)
            };
            ParticleModel = (EnumParticleModel)reader.ReadByte();
            VertexFlags = reader.ReadInt32();
            if (!reader.ReadBoolean())
            {
                OpacityEvolve = EvolvingNatFloat.CreateFromBytes(reader);
            }
            if (!reader.ReadBoolean())
            {
                RedEvolve = EvolvingNatFloat.CreateFromBytes(reader);
            }
            if (!reader.ReadBoolean())
            {
                GreenEvolve = EvolvingNatFloat.CreateFromBytes(reader);
            }
            if (!reader.ReadBoolean())
            {
                BlueEvolve = EvolvingNatFloat.CreateFromBytes(reader);
            }
            SizeEvolve.FromBytes(reader);
            SelfPropelled = reader.ReadBoolean();
            TerrainCollision = reader.ReadBoolean();
            ColorByBlock = reader.ReadBoolean();
            if (reader.ReadBoolean())
            {
                VelocityEvolve = new EvolvingNatFloat[]
                {
                    EvolvingNatFloat.createIdentical(0f),
                    EvolvingNatFloat.createIdentical(0f),
                    EvolvingNatFloat.createIdentical(0f)
                };
                VelocityEvolve[0].FromBytes(reader);
                VelocityEvolve[1].FromBytes(reader);
                VelocityEvolve[2].FromBytes(reader);
            }
            SecondarySpawnInterval = NatFloat.createFromBytes(reader);
            int secondaryPropCount = reader.ReadInt32();
            if (secondaryPropCount > 0)
            {
                SecondaryParticles = new AdvancedParticleProperties[secondaryPropCount];
                for (int i = 0; i < secondaryPropCount; i++)
                {
                    SecondaryParticles[i] = AdvancedParticleProperties.createFromBytes(reader, resolver);
                }
            }
            int deathPropCount = reader.ReadInt32();
            if (deathPropCount > 0)
            {
                DeathParticles = new AdvancedParticleProperties[deathPropCount];
                for (int j = 0; j < deathPropCount; j++)
                {
                    DeathParticles[j] = AdvancedParticleProperties.createFromBytes(reader, resolver);
                }
            }
            WindAffectedness = reader.ReadSingle();
            Bounciness = reader.ReadSingle();
        }

        public int GetRgbaColor(ICoreClientAPI capi)
        {
            if (HsvaColor is null)
            {
                return Color;
            }
            int num = ColorUtil.HsvToRgba(
                (int)((byte)GameMath.Clamp(HsvaColor[0].nextFloat(), 0f, 255f)), 
                (int)((byte)GameMath.Clamp(HsvaColor[1].nextFloat(), 0f, 255f)), 
                (int)((byte)GameMath.Clamp(HsvaColor[2].nextFloat(), 0f, 255f)), 
                (int)((byte)GameMath.Clamp(HsvaColor[3].nextFloat(), 0f, 255f)));
            int r = num & 255;
            int g = (num >> 8) & 255;
            int b = (num >> 16) & 255;
            int a = (num >> 24) & 255;
            return (r << 16) | (g << 8) | b | (a << 24);
        }

        public Vec3f GetVelocity(Vec3d pos)
        {
            tmpVelo.Set(
                baseVelocity.X + Velocity[0].nextFloat(),
                baseVelocity.Y + Velocity[1].nextFloat(),
                baseVelocity.Z + Velocity[2].nextFloat());

            return tmpVelo;
        }

        public void Init(ICoreAPI api) {}

        public void PrepareForSecondarySpawn(ParticleBase particleInstance)
        {
            Vec3d particlePos = particleInstance.Position;
            basePos.X = particlePos.X;
            basePos.Y = particlePos.Y;
            basePos.Z = particlePos.Z;
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(basePos.X);
            writer.Write(basePos.Y);
            writer.Write(basePos.Z);
            writer.Write(DieInAir);
            writer.Write(DieInLiquid);
            writer.Write(SwimOnLiquid);
            for (int i = 0; i < 4; i++)
            {
                HsvaColor[i].ToBytes(writer);
            }
            GravityEffect.ToBytes(writer);
            LifeLength.ToBytes(writer);
            for (int j = 0; j < 3; j++)
            {
                PosOffset[j].ToBytes(writer);
            }
            Quantity.ToBytes(writer);
            Size.ToBytes(writer);
            for (int k = 0; k < 3; k++)
            {
                Velocity[k].ToBytes(writer);
            }
            writer.Write((byte)ParticleModel);
            writer.Write(VertexFlags);
            writer.Write(OpacityEvolve == null);
            if (OpacityEvolve != null)
            {
                OpacityEvolve.ToBytes(writer);
            }
            writer.Write(RedEvolve == null);
            if (RedEvolve != null)
            {
                RedEvolve.ToBytes(writer);
            }
            writer.Write(GreenEvolve == null);
            if (GreenEvolve != null)
            {
                GreenEvolve.ToBytes(writer);
            }
            writer.Write(BlueEvolve == null);
            if (BlueEvolve != null)
            {
                BlueEvolve.ToBytes(writer);
            }
            SizeEvolve.ToBytes(writer);
            writer.Write(SelfPropelled);
            writer.Write(TerrainCollision);
            writer.Write(ColorByBlock);
            writer.Write(VelocityEvolve != null);
            if (VelocityEvolve != null)
            {
                for (int l = 0; l < 3; l++)
                {
                    VelocityEvolve[l].ToBytes(writer);
                }
            }
            SecondarySpawnInterval.ToBytes(writer);
            if (SecondaryParticles == null)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(SecondaryParticles.Length);
                for (int m = 0; m < SecondaryParticles.Length; m++)
                {
                    SecondaryParticles[m].ToBytes(writer);
                }
            }
            if (DeathParticles == null)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(DeathParticles.Length);
                for (int n = 0; n < DeathParticles.Length; n++)
                {
                    DeathParticles[n].ToBytes(writer);
                }
            }
            writer.Write(WindAffectedness);
            writer.Write(Bounciness);
        }

        public AsyncAdvancedParticleProperties Clone()
        {
            AsyncAdvancedParticleProperties cloned = new();

            using (MemoryStream ms = new())
            {
                BinaryWriter writer = new(ms);
                ToBytes(writer);
                ms.Position = 0;
                cloned.FromBytes(new(ms), null);
            }

            return cloned;
        }

        public static AsyncAdvancedParticleProperties createFromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            AsyncAdvancedParticleProperties properties = new();
            properties.FromBytes(reader, resolver);
            return properties;
        }
    }
}