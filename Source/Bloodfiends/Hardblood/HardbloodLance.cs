using HarmonyLib;
using RimWorld;
using SingularityFramework.Geometry;
using SingularityFramework.Sequences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public Vector2 gripOnSprite = new Vector2(-0.48f, 0f);
        public Vector2 tipOnSprite = new Vector2(0.48f, 0f);
        public float formSeconds = 0.8f;
        public float formLineWidth = 0.24f;

        public float reachRange = 2.9f;                    // its melee attacks land this far away (vanilla melee is adjacent only)

        public float chargeMinimumRange = 3f;
        public float chargeMaximumRange = 8f;
        public int cellsPastTarget = 2;
        public int chargeWindupTicks = 22;                 // the lance raised overhead before the slide (the animation's raise and hold)
        public CompProperties_AbilityLineDash charge;
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
                HardbloodLanceExtension lance = HardbloodLance.LanceOf(pawn);
                if (lance == null) continue;
                JobDef currentJob = pawn.CurJobDef;
                if (currentJob == JobDefOf.AttackMelee) TryCharge(pawn, lance);
                else if (currentJob == JobDefOf.Wait_Combat) TryAutoAttackInReach(pawn, lance);
            }
        }

        /// <summary>
        /// A Kindred chasing a target that's in charge range charges it instead. The vanilla attack is queued to pick up
        /// right where the charge ends.
        /// </summary>
        private void TryCharge(Pawn pawn, HardbloodLanceExtension lance)
        {
            Job chase = pawn.CurJob;
            if (lance.charge == null || pawn.stances.FullBodyBusy || !(chase.targetA.Thing is Pawn target)) return;
            if (!target.Spawned || target.Map != map || target.Downed) return;
            float distance = pawn.Position.DistanceTo(target.Position);
            if (distance < lance.chargeMinimumRange || distance > lance.chargeMaximumRange) return;
            IntVec3 endCell = HardbloodLance.ChargeEndCell(pawn, target, lance);
            if (!endCell.IsValid) return;

            Job continuation = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            continuation.playerForced = chase.playerForced;
            continuation.killIncappedTarget = chase.killIncappedTarget;
            continuation.maxNumMeleeAttacks = chase.maxNumMeleeAttacks;
            Job charge = JobMaker.MakeJob(BloodfiendsDefOf.Bloodfiends_HardbloodLanceCharge, endCell, target);
            charge.playerForced = chase.playerForced;
            pawn.jobs.jobQueue.EnqueueFirst(continuation);
            pawn.jobs.StartJob(charge, JobCondition.InterruptForced);
        }

        /// <summary>
        /// Vanilla's drafted auto-attack (JobDriver_Wait.CheckForAutoAttack) only swings at adjacent enemies; this is
        /// the same swing for enemies the lance can reach.
        /// </summary>
        private void TryAutoAttackInReach(Pawn pawn, HardbloodLanceExtension lance)
        {
            if (pawn.Faction == null || pawn.Downed || pawn.stances.FullBodyBusy || pawn.WorkTagIsDisabled(WorkTags.Violent)) return;
            if (pawn.drafter != null && !pawn.drafter.FireAtWill) return;

            Pawn nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawn.Position, map, lance.reachRange, false))
            {
                if (!(thing is Pawn enemy) || enemy.Dead || enemy.Downed || !enemy.HostileTo(pawn)) continue;
                float distance = pawn.Position.DistanceTo(enemy.Position);
                if (distance >= nearestDistance || pawn.CanReachImmediate(enemy, PathEndMode.Touch) || !HardbloodLance.WithinReach(pawn, pawn.Position, enemy)) continue;
                nearest = enemy;
                nearestDistance = distance;
            }
            if (nearest != null) pawn.meleeVerbs.TryMeleeAttack(nearest);
        }
    }

    public static class HardbloodLance
    {
        public static HardbloodLanceExtension LanceOf(Pawn pawn) => pawn?.equipment?.Primary?.def.GetModExtension<HardbloodLanceExtension>();

        /// <summary>
        /// Whether a Kindred standing at root can hit the target with its lance: within reach, with nothing in the way.
        /// </summary>
        public static bool WithinReach(Pawn pawn, IntVec3 root, Thing target)
        {
            HardbloodLanceExtension lance = LanceOf(pawn);
            if (lance == null || target == null || !target.Spawned || target.Map != pawn.Map) return false;
            if (target is Pawn targetPawn && targetPawn.IsPsychologicallyInvisible()) return false;
            IntVec3 targetCell = target.OccupiedRect().ClosestCellTo(root);
            return root.DistanceTo(targetCell) <= lance.reachRange && GenSight.LineOfSight(root, targetCell, pawn.Map);
        }

        /// <summary>
        /// The charge's strike, dealt with the lance's point: the same damage and armour penetration its normal stab
        /// deals (quality and the Kindred's melee damage included). The def's settings are kept for everything else.
        /// </summary>
        public static LineStrikeSettings ChargeStrikeFor(Pawn pawn, LineStrikeSettings baseSettings)
        {
            LineStrikeSettings settings = baseSettings.Copy();
            Verb point = PointVerbOf(pawn);
            if (point == null) return settings;
            settings.damageDefinition = point.verbProps.meleeDamageDef ?? settings.damageDefinition;
            settings.damageAmount = point.verbProps.AdjustedMeleeDamageAmount(point, pawn);
            settings.armorPenetration = point.verbProps.AdjustedArmorPenetration(point, pawn);
            return settings;
        }

        private static Verb PointVerbOf(Pawn pawn)
        {
            List<Verb> verbs = pawn.equipment?.PrimaryEq?.AllVerbs;
            if (verbs == null) return null;
            return verbs
                .Where(verb => verb.tool?.capacities != null)
                .FirstOrDefault(verb => verb.tool.capacities.Any(capacity => capacity.defName == "Stab"));
        }

        public static IntVec3 ChargeEndCell(Pawn pawn, Pawn target, HardbloodLanceExtension lance)
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

        private LineStrikeSettings strikeFromPoint;   // worked out once per charge

        protected override LineStrikeSettings StrikeSettings => strikeFromPoint ?? (strikeFromPoint = HardbloodLance.ChargeStrikeFor(pawn, DashProps.strikeSettings));

        protected override LocalTargetInfo MainTarget => job.targetB;   // targetA is the cell it ends on

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => HardbloodLance.LanceOf(pawn) == null);
            int windupTicks = HardbloodLance.LanceOf(pawn)?.chargeWindupTicks ?? 0;
            if (windupTicks > 0)
            {
                Toil windUp = Toils_General.Wait(windupTicks);
                windUp.handlingFacing = true;
                windUp.AddPreInitAction(() => pawn.pather.StopDead());
                windUp.AddPreTickAction(FaceCharge);
                yield return windUp;
            }
            yield return Toils_General.Do(AimAtTarget);
            foreach (Toil toil in base.MakeNewToils())
            {
                toil.handlingFacing = true;
                toil.AddPreTickAction(FaceCharge);
                yield return toil;
            }
        }

        /// <summary>
        /// Faces the target while winding up, then down the line it's sliding along, so it never turns around after
        /// piercing through.
        /// </summary>
        private void FaceCharge()
        {
            if (Dashing) pawn.rotationTracker.FaceCell(job.targetA.Cell);
            else if (job.targetB.IsValid) pawn.rotationTracker.FaceTarget(job.targetB);
        }

        /// <summary>
        /// The target kept moving during the wind-up, so the line is picked again from where it stands now. If it's
        /// out of charge range now, the charge is called off and the queued vanilla attack takes over.
        /// </summary>
        private void AimAtTarget()
        {
            HardbloodLanceExtension lance = HardbloodLance.LanceOf(pawn);
            Pawn target = job.targetB.Pawn;
            if (lance == null || target == null || target.Dead || !target.Spawned || target.Map != pawn.Map)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            float distance = pawn.Position.DistanceTo(target.Position);
            bool inChargeRange = distance >= lance.chargeMinimumRange && distance <= lance.chargeMaximumRange;
            IntVec3 endCell = inChargeRange ? HardbloodLance.ChargeEndCell(pawn, target, lance) : IntVec3.Invalid;
            if (!endCell.IsValid)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            job.targetA = endCell;
            pawn.rotationTracker.FaceTarget(target);
            pawn.Drawer.Notify_MeleeAttackOn(target);
        }
    }

    /// <summary>
    /// The vanilla attack job walks until the target is adjacent, then hits. A Kindred with the lance stops and hits
    /// as soon as the target is in reach; otherwise vanilla runs as normal (including chasing a target that leaves).
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ToilsCombat_FollowAndMeleeAttack_HardbloodLance
    {
        private static FieldInfo toilField;
        private static FieldInfo hitActionField;
        private static FieldInfo targetIndexField;
        private static FieldInfo standPositionIndexField;

        public static MethodBase TargetMethod()
        {
            Type closureType = typeof(Toils_Combat).GetNestedTypes(AccessTools.all)
                .First(type => AccessTools.Field(type, "hitAction") != null && AccessTools.Field(type, "followAndAttack") != null);
            toilField = AccessTools.Field(closureType, "followAndAttack");
            hitActionField = AccessTools.Field(closureType, "hitAction");
            targetIndexField = AccessTools.Field(closureType, "targetInd");
            standPositionIndexField = AccessTools.Field(closureType, "standPositionInd");
            return AccessTools.GetDeclaredMethods(closureType)
                .First(method => method.Name.Contains("FollowAndMeleeAttack"));
        }

        public static bool Prefix(object __instance)
        {
            Pawn pawn = ((Toil)toilField.GetValue(__instance))?.actor;
            if (HardbloodLance.LanceOf(pawn) == null) return true;
            Job job = pawn.CurJob;
            if (job.GetTarget((TargetIndex)standPositionIndexField.GetValue(__instance)).IsValid) return true;   // told where to stand: vanilla
            Thing target = job.GetTarget((TargetIndex)targetIndexField.GetValue(__instance)).Thing;
            if (target == null || pawn.CanReachImmediate(target, PathEndMode.Touch)) return true;                 // adjacent: vanilla
            if (target is Pawn targetPawn && targetPawn.Downed && !job.killIncappedTarget) return true;            // vanilla ends the attack
            if (!HardbloodLance.WithinReach(pawn, pawn.Position, target)) return true;                             // out of reach: vanilla walks closer

            if (pawn.pather.Moving) pawn.pather.StopDead();
            ((Action)hitActionField.GetValue(__instance)).Invoke();
            return false;
        }
    }

    /// <summary>
    /// Vanilla melee verbs can only hit adjacent targets; the lance's own verbs can hit anything within its reach.
    /// </summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
    public static class Patch_Verb_TryFindShootLineFromTo_HardbloodLance
    {
        public static void Postfix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, ref bool __result)
        {
            if (__result || !__instance.verbProps.IsMeleeAttack || !targ.HasThing || !(__instance.caster is Pawn pawn)) return;
            if (__instance.EquipmentSource?.def.GetModExtension<HardbloodLanceExtension>() == null) return;
            if (!HardbloodLance.WithinReach(pawn, root, targ.Thing)) return;
            resultingLine = new ShootLine(root, targ.Cell);
            __result = true;
        }
    }

    /// <summary>
    /// A melee attack picks from all the pawn's melee verbs (kicks, head butts too). From beyond arm's length, only the
    /// lance's own attacks reach, so one of those is picked, weighted the same way vanilla weighs them.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
    public static class Patch_PawnMeleeVerbs_TryGetMeleeVerb_HardbloodLance
    {
        public static void Postfix(Pawn_MeleeVerbs __instance, Thing target, ref Verb __result)
        {
            Pawn pawn = __instance.Pawn;
            ThingWithComps weapon = pawn?.equipment?.Primary;
            if (target == null || HardbloodLance.LanceOf(pawn) == null || __result?.EquipmentSource == weapon) return;
            if (!pawn.Spawned || pawn.CanReachImmediate(target, PathEndMode.Touch)) return;

            bool found = __instance.GetUpdatedAvailableVerbsList(false)
                .Where(entry => entry.verb.EquipmentSource == weapon)
                .TryRandomElementByWeight(entry => entry.GetSelectionWeight(target), out VerbEntry chosen);
            if (found) __result = chosen.verb;
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