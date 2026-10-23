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

    public class BloodbagExtension : DefModExtension
    {
        public float hemogenMaxOffset = 2f;   // added to the hemogen maximum when they turn (1 = 100 hemogen)
        public float hemogenPerDamage = 0.005f;   // hemogen drained per point of melee damage dealt (0.005 = 0.5 hemogen)
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
