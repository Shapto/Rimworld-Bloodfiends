using Bloodfiends.Family;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.SharedThirst
{
    public static class FamilialImpiety
    {
        private const int HeaviestImpietyGap = 3;
        public static void TryApply(Pawn attacker, Pawn victim)
        {
            if (attacker == null || victim == null || attacker == victim || attacker.Dead) return;
            if (attacker.genes?.GetFirstGeneOfType<Gene_SharedThirst>()?.Active != true) return;
            GameComponent_BloodfiendFamilies families = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode attackerNode = families?.GetNodeForPawn(attacker);
            BloodfiendFamilyNode victimNode = families?.GetNodeForPawn(victim);
            if (attackerNode == null || victimNode == null || attackerNode.familyId != victimNode.familyId) return;

            // positive: the attacker sits lower than the one they struck (a higher generation number is a lower tier)
            int tierGap = attackerNode.generationNumber - victimNode.generationNumber;
            if (tierGap < 0)
            {
                victim.needs?.mood?.thoughts.memories.TryGainMemory(BloodfiendsDefOf.Bloodfiends_ChastisedByElder, attacker);
                return;
            }
            if (tierGap == 0)
            {
                attacker.needs?.mood?.thoughts.memories.TryGainMemory(BloodfiendsDefOf.Bloodfiends_SiblingQuarrel, victim);
                return;
            }

            Thought_Memory impiety = ThoughtMaker.MakeThought(BloodfiendsDefOf.Bloodfiends_FamilialImpiety, Mathf.Min(tierGap, HeaviestImpietyGap) - 1);
            attacker.needs?.mood?.thoughts.memories.TryGainMemory(impiety, victim);
            victim.needs?.mood?.thoughts.memories.TryGainMemory(BloodfiendsDefOf.Bloodfiends_StruckByLesserKindred, attacker);
        }

        public static void Notify_ElderKilled(Pawn elder, BloodfiendFamilyNode elderNode, DamageInfo? damageInfo)
        {
            if (!(damageInfo?.Instigator is Pawn killer) || killer == elder || killer.Dead) return;
            BloodfiendFamilyNode killerNode = GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(killer);
            if (killerNode == null || killerNode.familyId != elderNode.familyId || killerNode.generationNumber <= elderNode.generationNumber) return;
            killer.needs?.mood?.thoughts.memories.TryGainMemory(BloodfiendsDefOf.Bloodfiends_KilledElder, elder);
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
