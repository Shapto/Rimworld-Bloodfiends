using Bloodfiends.Family;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.SharedThirst
{
    public static class FamilialImpiety
    {
        public static void TryApply(Pawn attacker, Pawn victim)
        {
            if (attacker == null || victim == null || attacker == victim || attacker.Dead) return;
            if (attacker.genes?.GetFirstGeneOfType<Gene_SharedThirst>()?.Active != true) return;
            if (GameComponent_BloodfiendFamilies.Instance?.AreSameFamily(attacker, victim) != true) return;

            attacker.needs?.mood?.thoughts.memories.TryGainMemory(BloodfiendsDefOf.Bloodfiends_FamilialImpiety, victim);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
    public static class Patch_PawnHealthTracker_PostApplyDamage
    {
        public static void Postfix(Pawn ___pawn, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || dinfo.IntendedTarget != ___pawn) return;
            FamilialImpiety.TryApply(dinfo.Instigator as Pawn, ___pawn);
        }
    }

}
