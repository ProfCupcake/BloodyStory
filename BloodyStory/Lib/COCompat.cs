using Vintagestory.API.Common;
using Vintagestory.API.Server;
using CombatOverhaul.DamageSystems;
using CombatOverhaul.Colliders;
using Vintagestory.API.Common.Entities;

namespace BloodyStory.Lib
{
    static class COCompat
    {
        public static void AddCODamageEH_Player(EntityPlayer player, EntityBehaviorBleed bleedEB)
        {
            PlayerDamageModelBehavior pDamageModel = player.GetBehavior<PlayerDamageModelBehavior>();

            pDamageModel.OnReceiveDamage += (ref float dmg, DamageSource dmgSrc, PlayerBodyPart bodyPart) =>
            {
                dmg = bleedEB.HandleDamage(dmg, dmgSrc);
            };

            EntityBehaviorHealth_BS pHealth = player.GetBehavior<EntityBehaviorHealth_BS>();

            pHealth.onDamagedPost += (dmg, dmgSrc) =>
            {
                if (dmgSrc.Type == EnumDamageType.BluntAttack
                    || dmgSrc.Type == EnumDamageType.SlashingAttack
                    || dmgSrc.Type == EnumDamageType.PiercingAttack) return dmg;
                return bleedEB.HandleDamage(dmg, dmgSrc); 
            };
        }

        public static void AddCODamageEH_NPC(Entity entity, EntityBehaviorBleed bleedEB)
        {
            EntityDamageModelBehavior eDamageModel = entity.GetBehavior<EntityDamageModelBehavior>();

            EntityBehaviorHealth_BS pHealth = entity.GetBehavior<EntityBehaviorHealth_BS>();

            if (eDamageModel is not null)
            {

                eDamageModel.OnReceiveDamage += (ref float damage, DamageSource dmgSrc, ColliderTypes damageZone, string collider) =>
                {
                    damage = bleedEB.HandleDamage(damage, dmgSrc);
                };

                pHealth.onDamagedPost += (dmg, dmgSrc) =>
                {
                    if (dmgSrc.Type == EnumDamageType.BluntAttack
                        || dmgSrc.Type == EnumDamageType.SlashingAttack
                        || dmgSrc.Type == EnumDamageType.PiercingAttack) return dmg;
                    return bleedEB.HandleDamage(dmg, dmgSrc); 
                };
            } else
            {
                pHealth.onDamagedPost += bleedEB.HandleDamage;
            }
        }
    };
}
