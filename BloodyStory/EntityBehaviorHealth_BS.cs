using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace BloodyStory
{
    class EntityBehaviorHealth_BS : EntityBehaviorHealth
    {
        public EntityBehaviorHealth_BS(Entity entity) : base(entity)
        {
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
            if (ebh != null && ebh != this) entity.RemoveBehavior(ebh);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            entity.Api.Logger.Debug("woah check it out I logged [BSEBH]");
        }
    }
}
