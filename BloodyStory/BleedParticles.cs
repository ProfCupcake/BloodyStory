using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BloodyStory
{
    [ProtoContract(ImplicitFields=ImplicitFields.AllFields)]
    internal class BleedParticles : AdvancedParticleProperties
    {
        public BleedParticles()
        {
            HsvaColor = new NatFloat[]
                {
                    NatFloat.Zero,
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(255f,0f),
                    NatFloat.createUniform(255f,0f)
                };
            LifeLength = NatFloat.One;
            GravityEffect = NatFloat.One;
            Size = NatFloat.createUniform(0.35f, 0.15f);
            DieInLiquid = true;
            DeathParticles = new AdvancedParticleProperties[]{
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
            ParticleModel = EnumParticleModel.Cube;
        }
    }
}