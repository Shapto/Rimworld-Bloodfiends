using HarmonyLib;
using RimWorld;
using SingularityFramework.Geometry;
using SingularityFramework.Sequences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Bloodfiends.Hardblood
{
    public class HardbloodLanceExtension : DefModExtension
    {
        public int maximumGeneration = 2;                  // Second Kindred and above can manifest it
        public float manifestCost = 0.8f;                  // 80 hemogen
        public float hemogenPerDamage = 0.01f;             // blood drunk back per point of damage it deals
        public Vector2 gripOnSprite = new Vector2(-0.14f, 0f);
        public Vector2 tipOnSprite = new Vector2(0.48f, 0f);

        public float reachRange = 2.9f;
        public DamageDef thrustDamageDef;
        public float thrustDamage = 38f;
        public float thrustArmorPenetration = 0.8f;
        public int thrustWarmupTicks = 24;
        public int thrustCooldownTicks = 120;

        public float chargeMinimumRange = 3f;
        public float chargeMaximumRange = 8f;
        public int cellsPastTarget = 2;
        public CompProperties_AbilityLineDash charge;

        public int chargeWindupTicks = 22;                  // the lance raised overhead before the slide (the animation's raise and hold)
    }

    public class MapComponent_HardbloodLances : MapComponent
    {
        private const int CheckIntervalTicks = 6;

        public MapComponent_HardbloodLances(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckIntervalTicks != 0) return;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                JobDef currentJob = pawn.CurJobDef;
                if (currentJob != JobDefOf.AttackMelee && currentJob != JobDefOf.Wait_Combat) continue;
                HardbloodLanceExtension lance = HardbloodLance.LanceOf(pawn);
                if (lance == null) continue;

                Job attack = currentJob == JobDefOf.AttackMelee ? WalkingAttack(pawn, lance) : StandingAttack(pawn, lance);
                if (attack == null) continue;
                attack.playerForced = pawn.CurJob.playerForced;
                pawn.jobs.StartJob(attack, JobCondition.InterruptForced);
            }
        }

        private Job WalkingAttack(Pawn pawn, HardbloodLanceExtension lance)
        {
            Pawn target = pawn.CurJob.targetA.Pawn;
            if (target == null || !target.Spawned || target.Map != map) return null;
            return HardbloodLance.AttackJobFor(pawn, target, lance);
        }

        private Job StandingAttack(Pawn pawn, HardbloodLanceExtension lance)
        {
            if (pawn.Downed || pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.stances.FullBodyBusy) return null;
            if (pawn.Drafted && !pawn.drafter.FireAtWill) return null;

            Pawn nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawn.Position, map, lance.reachRange, false))
            {
                if (!(thing is Pawn enemy) || enemy.Dead || enemy.Downed || !enemy.HostileTo(pawn)) continue;
                float distance = pawn.Position.DistanceTo(enemy.Position);
                if (distance <= 1.5f || distance >= nearestDistance || !GenSight.LineOfSight(pawn.Position, enemy.Position, map)) continue;
                nearest = enemy;
                nearestDistance = distance;
            }
            return nearest == null ? null : JobMaker.MakeJob(BloodfiendsDefOf.Bloodfiends_HardbloodLanceThrust, nearest);
        }
    }

    public static class HardbloodLance
    {
        public static HardbloodLanceExtension LanceOf(Pawn pawn) => pawn?.equipment?.Primary?.def.GetModExtension<HardbloodLanceExtension>();

        public static Job AttackJobFor(Pawn pawn, Pawn target, HardbloodLanceExtension lance)
        {
            float distance = pawn.Position.DistanceTo(target.Position);
            if (lance.charge != null && distance >= lance.chargeMinimumRange && distance <= lance.chargeMaximumRange)
            {
                IntVec3 endCell = ChargeEndCell(pawn, target, lance);
                if (endCell.IsValid) return JobMaker.MakeJob(BloodfiendsDefOf.Bloodfiends_HardbloodLanceCharge, endCell, target);
            }
            if (distance > 1.5f && distance <= lance.reachRange && GenSight.LineOfSight(pawn.Position, target.Position, pawn.Map))
            {
                return JobMaker.MakeJob(BloodfiendsDefOf.Bloodfiends_HardbloodLanceThrust, target);
            }
            return null;
        }

        private static IntVec3 ChargeEndCell(Pawn pawn, Pawn target, HardbloodLanceExtension lance)
        {
            Map map = pawn.Map;
            Vector3 direction = (target.Position - pawn.Position).ToVector3();
            direction.y = 0f;
            direction.Normalize();
            for (int cellsPast = lance.cellsPastTarget; cellsPast >= 1; cellsPast--)
            {
                IntVec3 cell = (target.Position.ToVector3Shifted() + direction * cellsPast).ToIntVec3();
                if (cell.InBounds(map) && cell.Standable(map) && LineStrike.IsLineWalkable(pawn.Position, cell, map, lance.charge.strikeSettings)) return cell;
            }
            return IntVec3.Invalid;
        }
    }

    public class JobDriver_HardbloodLanceCharge : JobDriver_LineDash
    {
        protected override CompProperties_AbilityLineDash DashProps => HardbloodLance.LanceOf(pawn)?.charge;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            int windupTicks = HardbloodLance.LanceOf(pawn)?.chargeWindupTicks ?? 0;
            if (windupTicks > 0)
            {
                Toil windUp = Toils_General.Wait(windupTicks);
                windUp.AddPreInitAction(() => pawn.pather.StopDead());
                windUp.AddPreTickAction(() => pawn.rotationTracker.FaceTarget(job.targetB.IsValid ? job.targetB : job.targetA));
                yield return windUp;
            }
            foreach (Toil toil in base.MakeNewToils()) yield return toil;
        }
    }

    public class JobDriver_HardbloodLanceThrust : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            HardbloodLanceExtension lance = HardbloodLance.LanceOf(pawn);
            this.FailOn(() => HardbloodLance.LanceOf(pawn) == null);

            Toil windUp = Toils_General.Wait(lance?.thrustWarmupTicks ?? 24);
            windUp.AddPreInitAction(() => pawn.pather.StopDead());
            windUp.AddPreTickAction(() => pawn.rotationTracker.FaceTarget(job.targetA));
            yield return windUp;
            yield return Toils_General.Do(Strike);
            yield return Toils_General.Wait(lance?.thrustCooldownTicks ?? 120);
        }

        private void Strike()
        {
            HardbloodLanceExtension lance = HardbloodLance.LanceOf(pawn);
            Pawn target = job.targetA.Pawn;
            if (lance == null || target == null || target.Dead || !target.Spawned) return;
            if (pawn.Position.DistanceTo(target.Position) > lance.reachRange + 0.5f) return;
            float angle = (target.Position - pawn.Position).AngleFlat;
            target.TakeDamage(new DamageInfo(lance.thrustDamageDef ?? DamageDefOf.Stab, lance.thrustDamage, lance.thrustArmorPenetration, angle, pawn, null, pawn.equipment?.Primary?.def));
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_PawnJobTracker_StartJob_HardbloodLance
    {
        public static void Prefix(Pawn ___pawn, ref Job newJob)
        {
            if (newJob == null || newJob.def != JobDefOf.AttackMelee || ___pawn == null || !___pawn.Spawned) return;
            HardbloodLanceExtension lance = HardbloodLance.LanceOf(___pawn);
            Pawn target = newJob.targetA.Pawn;
            if (lance == null || target == null || !target.Spawned || target.Map != ___pawn.Map) return;

            Job replacement = HardbloodLance.AttackJobFor(___pawn, target, lance);
            if (replacement == null) return;
            replacement.playerForced = newJob.playerForced;
            newJob = replacement;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
    public static class Patch_PawnHealthTracker_PostApplyDamage_HardbloodLance
    {
        public static void Postfix(DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || !(dinfo.Instigator is Pawn attacker)) return;
            HardbloodLanceExtension lance = dinfo.Weapon?.GetModExtension<HardbloodLanceExtension>();
            if (lance == null || attacker.genes?.GetFirstGeneOfType<Gene_Hemogen>() == null) return;
            GeneUtility.OffsetHemogen(attacker, totalDamageDealt * lance.hemogenPerDamage);
        }
    }
}
