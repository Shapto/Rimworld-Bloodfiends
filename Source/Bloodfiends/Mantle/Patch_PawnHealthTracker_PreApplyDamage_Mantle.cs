using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Mantle
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PreApplyDamage))]
    public static class Patch_PawnHealthTracker_PreApplyDamage_Mantle
    {
        [HarmonyPriority(Priority.High)]
        public static void Postfix(Pawn ___pawn, DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || ___pawn == null) return;
            Hediff_Mantle mantle = ___pawn.health.hediffSet.GetFirstHediff<Hediff_Mantle>();
            if (mantle != null && mantle.TryAbsorb(dinfo)) absorbed = true;
        }
    }
}
