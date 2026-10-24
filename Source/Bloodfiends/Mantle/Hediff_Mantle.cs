using Bloodfiends.BloodCommand;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Mantle
{
    public class MantleExtension : DefModExtension
    {
        public float cutFraction = 0.5f;                     // share of every hemogen gain that goes into the Mantle instead
        public float chargePerHemogen = 100f;                // charge per 1 hemogen (100 on the hemogen bar)
        public float maximumChargePerHemogenMaximum = 100f;  // maximum charge per 1 hemogen of the Bloodfiend's hemogen maximum
        public float chargeOnCast = 0.5f;                    // share of the maximum it has when cast
        public float minimumWallAngle = 35f;                 // degrees a new threat must be off the wall before the wall re-forms
        public float noticeRadius = 7f;                      // cells out it notices projectiles coming at the Bloodfiend
        public float wallHoldSeconds = 1.2f;                 // how long the wall holds after the last threat
    }

    public class Hediff_Mantle : HediffWithComps
    {
        private const int ThreatCheckIntervalTicks = 3;
        private const float WallRadius = 0.56f;
        private const float WallMiddleHeight = 0.26f;

        private static readonly MantleExtension DefaultSettings = new MantleExtension();

        public float charge;
        public bool removed;

        public MantleExtension Settings => def.GetModExtension<MantleExtension>() ?? DefaultSettings;

        public float MaximumCharge
        {
            get
            {
                Gene_Hemogen hemogenGene = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
                return (hemogenGene?.Max ?? 1f) * Settings.maximumChargePerHemogenMaximum;
            }
        }

        public float ChargeFraction => MaximumCharge > 0f ? Mathf.Clamp01(charge / MaximumCharge) : 0f;

        public override string LabelInBrackets
        {
            get
            {
                string chargeText = $"{Mathf.CeilToInt(charge)} / {Mathf.CeilToInt(MaximumCharge)}";
                string fromComps = base.LabelInBrackets;
                return fromComps.NullOrEmpty() ? chargeText : $"{chargeText}, {fromComps}";
            }
        }

        private MantleRibbons Ribbons => pawn.Spawned ? MapComponent_MantleRibbons.For(pawn.Map)?.RibbonsOf(this) : null;

        private Gizmo_MantleStatus statusGizmo;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (pawn.Faction != Faction.OfPlayer || Find.Selector.SingleSelectedThing != pawn) yield break;
            if (statusGizmo == null) statusGizmo = new Gizmo_MantleStatus { mantle = this };
            yield return statusGizmo;
        }

        public void Refresh()
        {
            charge = Mathf.Max(charge, MaximumCharge * Settings.chargeOnCast);
            HediffComp_Disappears disappears = this.TryGetComp<HediffComp_Disappears>();
            if (disappears != null) disappears.ticksToDisappear = disappears.Props.disappearsAfterTicks.RandomInRange;
        }

        public float TakeCut(float hemogenGained)
        {
            float roomInHemogen = Mathf.Max(0f, MaximumCharge - charge) / Settings.chargePerHemogen;
            float cut = Mathf.Min(hemogenGained * Settings.cutFraction, roomInHemogen);
            charge += cut * Settings.chargePerHemogen;
            return cut;
        }

        public bool TryAbsorb(DamageInfo damageInfo)
        {
            if (charge <= 0f || damageInfo.Def == null || !damageInfo.Def.harmsHealth || damageInfo.Def.armorCategory == null) return false;
            charge = Mathf.Max(0f, charge - damageInfo.Amount);
            float angle = AngleTowardSource(damageInfo);
            if (pawn.Spawned)
            {
                Ribbons?.Blocked(angle, Settings);
                FleckMaker.Static(WallPoint(angle), pawn.Map, FleckDefOf.ShotFlash, 0.6f);
                if (charge <= 0f) StartBreak(angle);
            }
            return true;
        }

        public override void PostTick()
        {
            base.PostTick();
            if (!pawn.Spawned || !pawn.IsHashIntervalTick(ThreatCheckIntervalTicks)) return;
            MantleRibbons ribbons = Ribbons;
            if (ribbons != null && charge > 0f && TryFindThreat(out float angle)) ribbons.Threat(angle, Settings);
        }

        private bool TryFindThreat(out float angle)
        {
            angle = 0f;
            bool found = false;
            float closestDistance = float.MaxValue;
            Vector3 position = pawn.DrawPos;
            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile))
            {
                if (!(thing is Projectile projectile) || projectile.Launcher == pawn) continue;
                Vector3 offset = position - projectile.ExactPosition;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > Settings.noticeRadius || distance >= closestDistance || distance < 0.01f) continue;
                Vector3 heading = projectile.ExactRotation * Vector3.forward;
                heading.y = 0f;
                if (Vector3.Dot(heading.normalized, offset / distance) < 0.97f) continue;
                closestDistance = distance;
                angle = Mathf.Atan2(-offset.z, -offset.x) * Mathf.Rad2Deg;
                found = true;
            }
            if (found) return true;

            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(pawn))
            {
                if (!cell.InBounds(pawn.Map)) continue;
                foreach (Thing thing in cell.GetThingList(pawn.Map))
                {
                    if (!(thing is Pawn attacker) || attacker.CurJobDef != JobDefOf.AttackMelee || attacker.CurJob.targetA.Thing != pawn) continue;
                    Vector3 offset = attacker.DrawPos - position;
                    angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
                    return true;
                }
            }
            return false;
        }

        private float AngleTowardSource(DamageInfo damageInfo)
        {
            Thing instigator = damageInfo.Instigator;
            if (instigator != null && instigator != pawn && instigator.Spawned && instigator.Map == pawn.Map)
            {
                Vector3 offset = instigator.DrawPos - pawn.DrawPos;
                return Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
            }
            Vector3 travel = Quaternion.AngleAxis(damageInfo.Angle, Vector3.up) * Vector3.forward;
            return Mathf.Atan2(-travel.z, -travel.x) * Mathf.Rad2Deg;
        }

        private Vector3 WallPoint(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return pawn.DrawPos + new Vector3(Mathf.Cos(radians) * WallRadius, 0f, Mathf.Sin(radians) * WallRadius * 0.2f + WallMiddleHeight);
        }

        private void StartBreak(float angle)
        {
            Map map = pawn.Map;
            float side = Mathf.Cos(angle * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            var effect = new ShatterLineEffect(MapComponent_BloodCommand.BloodPalette, WallPoint(angle), pawn.DrawPos, side, 6, 0.15f, 0.09f, pawn.thingIDNumber ^ Find.TickManager.TicksGame, 1.2f);
            effect.AtMoment(0.5f, () =>
            {
                foreach (Vector3 landing in effect.landingPoints)
                {
                    IntVec3 cell = landing.ToIntVec3();
                    if (cell.InBounds(map)) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood, 1);
                }
            });
            MapComponent_BrushLines.Start(map, effect);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            removed = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref charge, "charge");
        }
    }
}
