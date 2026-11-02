using HarmonyLib;
using Verse;

namespace Bloodfiends.SanguineRegeneration
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    public static class Patch_PawnHealthTracker_ShouldBeDead
    {
        public static void Postfix(Pawn_HealthTracker __instance, Pawn ___pawn, ref bool __result)
        {
            if (!__result) return;

            Gene_SanguineRegeneration regenerationGene = ___pawn?.genes?.GetFirstGeneOfType<Gene_SanguineRegeneration>();
            if (regenerationGene == null || !regenerationGene.Active) return;
            if (OnlyTooMuchDamage(__instance, ___pawn) || regenerationGene.TryPreventDeath()) __result = false;
        }

        /// <summary>
        /// Like Deathless, Sanguine Regeneration never dies from wounds piling up: the only thing killing this pawn is the
        /// total damage threshold, with nothing vital lost. It costs no hemogen and doesn't put them in the mortal state;
        /// they stay down until they've healed enough to get back up.
        /// </summary>
        private static bool OnlyTooMuchDamage(Pawn_HealthTracker health, Pawn pawn)
        {
            if (!health.ShouldBeDeadFromLethalDamageThreshold()) return false;
            if (health.hediffSet.GetBrain() == null) return false;
            foreach (Hediff hediff in health.hediffSet.hediffs)
                if (hediff.CauseDeathNow()) return false;
            if (health.ShouldBeDeadFromRequiredCapacity() != null) return false;
            return PawnCapacityUtility.CalculatePartEfficiency(health.hediffSet, pawn.RaceProps.body.corePart) > 0.0001f;
        }
    }
}