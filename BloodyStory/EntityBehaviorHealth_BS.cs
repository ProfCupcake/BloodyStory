using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace BloodyStory
{
    class EntityBehaviorHealth_BS : EntityBehaviorHealth
    {
        // additional onDamaged event running after the regular EBHealth onDamaged
        public event OnDamagedDelegate onDamagedPost = (float dmg, DamageSource dmgSource) => dmg;

        public EntityBehaviorHealth_BS(Entity entity) : base(entity)
        {
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (entity.World.Side == EnumAppSide.Client)
            {
                return;
            }
            float damageBeforeArmor = damage;

            MulticastDelegate onDamaged = Traverse.Create(this).Field("onDamaged").GetValue<MulticastDelegate>();
            if (onDamaged != null)
            {
                foreach (OnDamagedDelegate dele in onDamaged.GetInvocationList())
                {
                    damage = dele(damage, damageSource);
                }
            } else
            {
                entity.Api.Logger.Debug($"[BSEBH] Failed to acquire base onDamaged for {entity.GetName()}");
            }

            if (onDamagedPost != null)
            {
                foreach (OnDamagedDelegate dele in onDamagedPost.GetInvocationList())
                {
                    damage = dele(damage, damageSource);
                }
            }

            if (damageSource.Type == EnumDamageType.Heal)
            {
                if (damageSource.Source != EnumDamageSource.Revive)
                {
                    damage *= Math.Max(0f, entity.Stats.GetBlended("healingeffectivness"));
                    Health = Math.Min(Health + damage, MaxHealth);
                }
                else
                {
                    damage = Math.Min(damage, MaxHealth);
                    damage *= Math.Max(0.33f, entity.Stats.GetBlended("healingeffectivness"));
                    Health = damage;
                }
                entity.OnHurt(damageSource, damage);
                UpdateMaxHealth();
                return;
            }
            if (!entity.Alive)
            {
                return;
            }
            if (damage <= 0f)
            {
                return;
            }
            EntityPlayer player = entity as EntityPlayer;
            if (player != null)
            {
                EntityPlayer otherPlayer = damageSource.GetCauseEntity() as EntityPlayer;
                if (otherPlayer != null)
                {
                    string weapon;
                    if (damageSource.SourceEntity != otherPlayer)
                    {
                        weapon = damageSource.SourceEntity.Code.ToString();
                    }
                    else
                    {
                        ItemStack itemstack = otherPlayer.Player.InventoryManager.ActiveHotbarSlot.Itemstack;
                        weapon = ((itemstack != null) ? itemstack.Collectible.Code.ToString() : null) ?? "hands";
                    }
                    entity.Api.Logger.Audit("{0} at {1} got {2}/{3} damage {4} {5} by {6}", new object[]
                    {
                        player.Player.PlayerName,
                        entity.Pos.AsBlockPos,
                        damage,
                        damageBeforeArmor,
                        damageSource.Type.ToString().ToLowerInvariant(),
                        weapon,
                        otherPlayer.GetName()
                    });
                }
            }
            Health -= damage;
            entity.OnHurt(damageSource, damage);
            UpdateMaxHealth();
            if (Health <= 0f)
            {
                Health = 0f;
                entity.Die(EnumDespawnReason.Death, damageSource);
                return;
            }
            if (damage > 1f)
            {
                entity.AnimManager.StartAnimation("hurt");
            }
            if (damageSource.Type != EnumDamageType.Heal)
            {
                entity.PlayEntitySound("hurt", null, true, 24f);
            }
        }
    }
}
