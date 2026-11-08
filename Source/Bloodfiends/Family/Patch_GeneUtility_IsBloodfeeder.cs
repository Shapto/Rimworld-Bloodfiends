using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;

namespace Bloodfiends.Family
{
    [HarmonyPatch(typeof(GeneUtility), nameof(GeneUtility.IsBloodfeeder))]
    public static class Patch_GeneUtility_IsBloodfeeder
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result && pawn?.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active == true) __result = true;
        }
    }
}
