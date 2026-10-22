using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.SanguineRegeneration
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    public static class Patch_PawnHealthTracker_ShouldBeDead
    {
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (!__result) return;

            Gene_SanguineRegeneration regenerationGene = ___pawn?.genes?.GetFirstGeneOfType<Gene_SanguineRegeneration>();
            if (regenerationGene != null && regenerationGene.TryPreventDeath()) __result = false;
        }
    }
}
