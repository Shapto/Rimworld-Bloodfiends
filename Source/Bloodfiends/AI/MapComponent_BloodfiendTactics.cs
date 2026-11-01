using Bloodfiends.Coagulate;
using Bloodfiends.Family;
using Bloodfiends.Hardblood;
using Bloodfiends.Hunger;
using Bloodfiends.Mantle;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Bloodfiends.AI
{
    public class MapComponent_BloodfiendTactics : MapComponent
    {
        private const int CheckIntervalTicks = 60;
        private const float HardenBelowHealth = 0.75f;     // cast Hardblood armor once this hurt
        private const float BleedingToHarden = 0.1f;       // or bleeding this fast
        private const float MantleThreatRadius = 25f;      // an enemy gun this close makes them raise the Mantle

        public MapComponent_BloodfiendTactics(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if ((Find.TickManager.TicksGame + map.uniqueID) % CheckIntervalTicks != 0) return;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (!IsFightingBloodfiend(pawn) || pawn.stances.FullBodyBusy || pawn.CurJob?.ability != null) continue;
                if (TryManifestLance(pawn)) continue;
                if (pawn.health.hediffSet.GetFirstHediff<Hediff_Mantle>() == null && EnemyGunNearby(pawn) && TryCastOnSelf(pawn, BloodfiendsDefOf.Bloodfiends_Mantle)) continue;
                if (pawn.health.hediffSet.GetFirstHediff<Hediff_HardbloodShell>() == null && IsHurt(pawn)) TryCastOnSelf(pawn, BloodfiendsDefOf.Bloodfiends_Coagulate);
            }
        }

        private static bool IsFightingBloodfiend(Pawn pawn)
        {
            if (pawn.Dead || pawn.Downed || pawn.IsPrisoner) return false;
            if (pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active != true) return false;
            if (BloodfrenzyUtility.IsFrenzied(pawn)) return pawn.mindState?.enemyTarget != null || pawn.CurJobDef == JobDefOf.AttackMelee;
            if (pawn.IsColonistPlayerControlled || pawn.InMentalState) return false;
            Thing enemy = pawn.mindState?.enemyTarget;
            return enemy != null && enemy.Spawned && !enemy.Destroyed;
        }

        private static bool TryManifestLance(Pawn pawn)
        {
            ThingDef lanceDef = BloodfiendsDefOf.Bloodfiends_HardbloodLance;
            HardbloodLanceExtension lance = lanceDef?.GetModExtension<HardbloodLanceExtension>();
            if (lance == null || HardbloodWeaponry.GenerationOf(pawn) > lance.maximumGeneration) return false;
            if (pawn.equipment == null || pawn.equipment.Primary?.def == lanceDef) return false;
            if (!BloodfrenzyUtility.CanAfford(pawn, lance.manifestCost)) return false;
            pawn.genes.GetFirstGeneOfType<Gene_Bloodfiend>().weaponry.ManifestLance(pawn);
            return pawn.equipment.Primary?.def == lanceDef;
        }

        private static bool TryCastOnSelf(Pawn pawn, AbilityDef abilityDef)
        {
            Ability ability = pawn.abilities?.GetAbility(abilityDef);
            if (ability == null || !ability.CanCast || !ability.CanApplyOn((LocalTargetInfo)pawn)) return false;
            float hemogenCost = ability.EffectComps
                .OfType<CompAbilityEffect_HemogenCost>()
                .Sum(cost => cost.Props.hemogenCost);
            if (!BloodfrenzyUtility.CanAfford(pawn, hemogenCost)) return false;
            Job castJob = ability.GetJob(pawn, pawn);
            if (castJob == null) return false;
            pawn.jobs.StartJob(castJob, JobCondition.InterruptForced);
            if (BloodfrenzyUtility.IsFrenzied(pawn)) BloodfrenzyUtility.Pay(pawn, hemogenCost);
            return true;
        }

        private static bool IsHurt(Pawn pawn) => pawn.health.summaryHealth.SummaryHealthPercent < HardenBelowHealth || pawn.health.hediffSet.BleedRateTotal > BleedingToHarden;

        private bool EnemyGunNearby(Pawn pawn)
        {
            float radiusSquared = MantleThreatRadius * MantleThreatRadius;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn other = pawns[index];
                if (other.Dead || other.Downed || !other.HostileTo(pawn) || other.equipment?.Primary?.def.IsRangedWeapon != true) continue;
                if ((other.Position - pawn.Position).LengthHorizontalSquared <= radiusSquared) return true;
            }
            return false;
        }

        private static float HemogenOf(Pawn pawn) => pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>()?.Value ?? 0f;
    }
}