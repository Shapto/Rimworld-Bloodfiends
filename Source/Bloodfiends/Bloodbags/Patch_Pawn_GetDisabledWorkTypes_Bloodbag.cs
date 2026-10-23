using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Bloodbags
{
    public class MutantAllowedWorkExtension : DefModExtension
    {
        public List<WorkTypeDef> allowedWorkTypes = new List<WorkTypeDef>();
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    public static class Patch_Pawn_GetDisabledWorkTypes_Bloodbag
    {
        public static void Postfix(Pawn __instance, List<WorkTypeDef> __result)
        {
            if (__result == null || !__instance.IsMutant) return;
            MutantAllowedWorkExtension extension = __instance.mutant.Def.GetModExtension<MutantAllowedWorkExtension>();
            if (extension == null) return;
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!extension.allowedWorkTypes.Contains(workType) && !__result.Contains(workType)) __result.Add(workType);
            }
        }
    }
}
