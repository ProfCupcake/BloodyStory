using CombatOverhaul.DamageSystems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace BloodyStory
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
