using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace BloodyStory
{
    [HarmonyPatch]
    static class EBHealthPatches
    {

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Vintagestory.ServerMods.Core), "RegisterDefaultEntityBehaviors")]
        public static bool TheseAreMyEntityBehaviorsNowJon(Vintagestory.ServerMods.Core __instance)
        {
            ICoreServerAPI api = Traverse.Create(__instance).Field("api").GetValue<ICoreServerAPI>();

            api.RegisterEntityBehaviorClass("collectitems", typeof(EntityBehaviorCollectEntities));
            api.RegisterEntityBehaviorClass("health", typeof(EntityBehaviorHealth_BS));
            api.RegisterEntityBehaviorClass("hunger", typeof(EntityBehaviorHunger));
            api.RegisterEntityBehaviorClass("drunktyping", typeof(EntityBehaviorDrunkTyping));
            api.RegisterEntityBehaviorClass("breathe", typeof(EntityBehaviorBreathe));
            api.RegisterEntityBehaviorClass("playerphysics", typeof(EntityBehaviorPlayerPhysics));
            api.RegisterEntityBehaviorClass("controlledphysics", typeof(EntityBehaviorControlledPhysics));
            api.RegisterEntityBehaviorClass("taskai", typeof(EntityBehaviorTaskAI));
            api.RegisterEntityBehaviorClass("goalai", typeof(EntityBehaviorGoalAI));
            api.RegisterEntityBehaviorClass("interpolateposition", typeof(EntityBehaviorInterpolatePosition));
            api.RegisterEntityBehaviorClass("despawn", typeof(EntityBehaviorDespawn));
            api.RegisterEntityBehaviorClass("grow", typeof(EntityBehaviorGrow));
            api.RegisterEntityBehaviorClass("multiply", typeof(EntityBehaviorMultiply));
            api.RegisterEntityBehaviorClass("multiplybase", typeof(EntityBehaviorMultiplyBase));
            api.RegisterEntityBehaviorClass("aimingaccuracy", typeof(EntityBehaviorAimingAccuracy));
            api.RegisterEntityBehaviorClass("emotionstates", typeof(EntityBehaviorEmotionStates));
            api.RegisterEntityBehaviorClass("repulseagents", typeof(EntityBehaviorRepulseAgents));
            api.RegisterEntityBehaviorClass("ellipsoidalrepulseagents", typeof(EntityBehaviorEllipsoidalRepulseAgents));
            api.RegisterEntityBehaviorClass("tiredness", typeof(EntityBehaviorTiredness));
            api.RegisterEntityBehaviorClass("nametag", typeof(EntityBehaviorNameTag));
            api.RegisterEntityBehaviorClass("placeblock", typeof(EntityBehaviorPlaceBlock));
            api.RegisterEntityBehaviorClass("deaddecay", typeof(EntityBehaviorDeadDecay));
            api.RegisterEntityBehaviorClass("floatupwhenstuck", typeof(EntityBehaviorFloatUpWhenStuck));
            api.RegisterEntityBehaviorClass("harvestable", typeof(EntityBehaviorHarvestable));
            api.RegisterEntityBehaviorClass("reviveondeath", typeof(EntityBehaviorReviveOnDeath));
            api.RegisterEntityBehaviorClass("mouthinventory", typeof(EntityBehaviorMouthInventory));
            api.RegisterEntityBehaviorClass("openablecontainer", typeof(EntityBehaviorOpenableContainer));
            api.RegisterEntityBehaviorClass("playerinventory", typeof(EntityBehaviorPlayerInventory));

            api.Logger.Debug("Hey lookit, I replaced the default behaviors method [BSPATCH]");

            return false;
        }//*/


        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityBehaviorHealth), nameof(EntityBehaviorHealth.OnEntityReceiveDamage))]
        public static bool EBHealthOnReceiveDamagePatch(EntityBehaviorHealth __instance)
        {
            __instance.entity.Api.Logger.Debug("Entity received damage, and I sees it because I done a prefix! [BSPATCH]");

            return true;
        }//*/
    }

    [HarmonyPatch(typeof(Vintagestory.ServerMods.Core), "RegisterDefaultEntityBehaviors")]
    public static class DefaultEntityBehviors_Patch
    {
        public static ICoreAPI api;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            api.Logger.Debug("[BSEBH] Starting transpiler...");
            // Iterate through until entitybehaviorhealth is found
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldtoken)
                {
                    api.Logger.Debug("[BSEBH] Found EBH opcode...");
                    if (codes[i].OperandIs(typeof(EntityBehaviorHealth)))
                    {
                        codes[i].operand = typeof(EntityBehaviorHealth_BS);
                        api.Logger.Debug("[BSEBH] Replaced EBH opcode...");
                        break;
                    }
                }
            }

            api.Logger.Debug("[BSEBH] Transpiler returning.");
            return codes.AsEnumerable();
        }
    }
}
