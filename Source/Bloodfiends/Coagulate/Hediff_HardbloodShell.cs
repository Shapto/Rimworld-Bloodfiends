using Bloodfiends.BloodCommand;
using Bloodfiends.Hardblood;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Coagulate
{
    public class Hediff_HardbloodShell : HediffWithComps
    {
        public float shellPoints;
        public float maximumShellPoints;
        public float regenerationMultiplier = 1f;

        public override string LabelInBrackets
        {
            get
            {
                string points = $"{Mathf.CeilToInt(shellPoints)} / {Mathf.CeilToInt(maximumShellPoints)}";
                string fromComps = base.LabelInBrackets;
                return fromComps.NullOrEmpty() ? points : $"{points}, {fromComps}";
            }
        }

        public static float RegenerationMultiplierFor(Pawn pawn)
        {
            return pawn.health.hediffSet.GetFirstHediff<Hediff_HardbloodShell>()?.regenerationMultiplier ?? 1f;
        }

        public void Refill(float points, float multiplier)
        {
            maximumShellPoints = Mathf.Max(maximumShellPoints, points);
            shellPoints = Mathf.Max(shellPoints, points);
            regenerationMultiplier = Mathf.Max(regenerationMultiplier, multiplier);
            HediffComp_Disappears disappears = this.TryGetComp<HediffComp_Disappears>();
            if (disappears != null) disappears.ticksToDisappear = disappears.Props.disappearsAfterTicks.RandomInRange;
        }

        private bool brokenByDamage;

        public bool TryAbsorb(DamageInfo damageInfo)
        {
            if (shellPoints <= 0f || damageInfo.Def == null || !damageInfo.Def.harmsHealth || damageInfo.Def.armorCategory == null) return false;
            shellPoints -= damageInfo.Amount;
            if (pawn.Spawned) FleckMaker.Static(pawn.DrawPos, pawn.Map, FleckDefOf.ShotFlash, 0.6f);
            if (shellPoints <= 0f) Shatter();
            else BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodHit, pawn);
            return true;
        }

        private void Shatter()
        {
            brokenByDamage = true;
            pawn.health.RemoveHediff(this);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (pawn.Spawned)
            {
                StartShatter();
                if (brokenByDamage) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodShatter, pawn);
                else BloodfiendsSounds.PlayDissolve(pawn);
            }
            HardbloodGraphics.ForgetMask(pawn);
        }

        private void StartShatter()
        {
            Map map = pawn.Map;
            float side = HardbloodGraphics.MaskSide(pawn);
            Vector3 headPosition = pawn.DrawPos + pawn.Drawer.renderer.BaseHeadOffsetAt(pawn.Rotation);
            Vector3 source = headPosition + new Vector3(0.12f * side, 0f, 0f);
            var effect = new ShatterLineEffect(MapComponent_BloodCommand.BloodPalette, source, pawn.DrawPos, side, 7, 0.15f, 0.09f, pawn.thingIDNumber ^ Find.TickManager.TicksGame, 1.4f);
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shellPoints, "shellPoints");
            Scribe_Values.Look(ref maximumShellPoints, "maximumShellPoints");
            Scribe_Values.Look(ref regenerationMultiplier, "regenerationMultiplier", 1f);
        }
    }

    public class PawnRenderNode_HardbloodMask : PawnRenderNode
    {
        public PawnRenderNode_HardbloodMask(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn) => HardbloodGraphics.MaskGraphic(pawn);

        public override GraphicMeshSet MeshSetFor(Pawn pawn) => HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
    }
}
