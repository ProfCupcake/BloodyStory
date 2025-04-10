using Vintagestory.API.Common;
using Vintagestory.API.Server;
using CombatOverhaul.DamageSystems;

namespace BloodyStory.Lib
{
    static class COCompat
    {
        public static void AddCODamageEH(IServerPlayer player, EntityBehaviorBleed bleedEB)
        {
            PlayerDamageModelBehavior pDamageModel = player.Entity.GetBehavior<PlayerDamageModelBehavior>();

            pDamageModel.OnReceiveDamage += (ref float dmg, DamageSource dmgSrc, PlayerBodyPart bodyPart) =>
            {
                dmg = bleedEB.HandleDamage(player, dmg, dmgSrc);
            };
        }
    };
}
