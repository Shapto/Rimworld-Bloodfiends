using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Coagulate
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PreApplyDamage))]
    public static class Patch_PawnHealthTracker_PreApplyDamage_HardbloodShell
    {
        public static void Postfix(Pawn ___pawn, DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || ___pawn == null) return;
            Hediff_HardbloodShell shell = ___pawn.health.hediffSet.GetFirstHediff<Hediff_HardbloodShell>();
            if (shell != null && shell.TryAbsorb(dinfo)) absorbed = true;
        }
    }
}
