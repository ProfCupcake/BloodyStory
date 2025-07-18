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
