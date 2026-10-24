using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Mantle
{
    [HarmonyPatch(typeof(GeneUtility), nameof(GeneUtility.OffsetHemogen))]
    public static class Patch_GeneUtility_OffsetHemogen_MantleCut
    {
        public static void Prefix(Pawn pawn, ref float offset, bool applyStatFactor)
        {
            if (offset <= 0f || !applyStatFactor || pawn?.health == null) return;
            Hediff_Mantle mantle = pawn.health.hediffSet.GetFirstHediff<Hediff_Mantle>();
            if (mantle != null) offset -= mantle.TakeCut(offset);
        }
    }
}
