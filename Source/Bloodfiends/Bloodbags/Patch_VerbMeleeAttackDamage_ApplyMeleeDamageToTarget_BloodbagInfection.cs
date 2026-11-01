using HarmonyLib;
using RimWorld;
using Verse;

namespace Bloodfiends.Bloodbags
{
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_VerbMeleeAttackDamage_ApplyMeleeDamageToTarget_BloodbagInfection
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target, DamageWorker.DamageResult __result)
        {
            // bounced off, deflected or absorbed: no blood got out or in
            if (__result == null || __result.deflected || __result.totalDamageDealt <= 0f) return;
            Pawn attacker = __instance.CasterPawn;
            if (!BloodbagUtility.IsBloodbag(attacker) || !(target.Thing is Pawn victim)) return;

            StealHemogen(attacker, victim, __result.totalDamageDealt);
            TryInfect(attacker, victim);
        }

        private static void StealHemogen(Pawn attacker, Pawn victim, float damage)
        {
            if (victim.RaceProps.BloodDef == null) return;   // nothing to drain from a mechanoid
            float hemogenPerDamage = BloodbagUtility.BloodbagDef?.GetModExtension<BloodbagExtension>()?.hemogenPerDamage ?? 0.005f;
            GeneUtility.OffsetHemogen(attacker, damage * hemogenPerDamage);
        }

        private static void TryInfect(Pawn attacker, Pawn victim)
        {
            if (!BloodbagUtility.CanBecomeBloodbag(victim) || victim.health.hediffSet.HasHediff(BloodfiendsDefOf.Bloodfiends_BloodbagInfection)) return;

            float chance = BloodfiendsDefOf.Bloodfiends_BloodbagInfection.GetModExtension<BloodbagInfectionExtension>()?.chancePerHit ?? 0.1f;
            if (!Rand.Chance(chance)) return;

            var infection = (Hediff_BloodbagInfection)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_BloodbagInfection, victim);
            infection.familyId = BloodbagUtility.FamilyOf(attacker);
            infection.master = BloodbagUtility.MasterFor(attacker);
            infection.faction = attacker.Faction;
            victim.health.AddHediff(infection);
        }
    }
}