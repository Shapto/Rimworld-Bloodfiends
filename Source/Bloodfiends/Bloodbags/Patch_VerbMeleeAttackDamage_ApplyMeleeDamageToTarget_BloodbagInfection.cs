using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Bloodbags
{
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_VerbMeleeAttackDamage_ApplyMeleeDamageToTarget_BloodbagInfection
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            Pawn attacker = __instance.CasterPawn;
            if (!BloodbagUtility.IsBloodbag(attacker) || !(target.Thing is Pawn victim) || !BloodbagUtility.CanBecomeBloodbag(victim)) return;
            if (victim.health.hediffSet.HasHediff(BloodfiendsDefOf.Bloodfiends_BloodbagInfection)) return;

            float chance = BloodfiendsDefOf.Bloodfiends_BloodbagInfection.GetModExtension<BloodbagInfectionExtension>()?.chancePerHit ?? 0.1f;
            if (!Rand.Chance(chance)) return;

            var infection = (Hediff_BloodbagInfection)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_BloodbagInfection, victim);
            infection.familyId = BloodbagUtility.FamilyOf(attacker);
            infection.faction = attacker.Faction;
            victim.health.AddHediff(infection);
        }
    }
}
