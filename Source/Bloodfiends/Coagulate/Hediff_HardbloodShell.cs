using Bloodfiends.Hardblood;
using RimWorld;
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

        public override string LabelInBrackets => $"{Mathf.CeilToInt(shellPoints)} / {Mathf.CeilToInt(maximumShellPoints)}";

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

        public bool TryAbsorb(DamageInfo damageInfo)
        {
            if (shellPoints <= 0f || damageInfo.Def == null || !damageInfo.Def.harmsHealth || damageInfo.Def.armorCategory == null) return false;
            shellPoints -= damageInfo.Amount;
            if (pawn.Spawned) FleckMaker.Static(pawn.DrawPos, pawn.Map, FleckDefOf.ShotFlash, 0.6f);
            if (shellPoints <= 0f) Shatter();
            return true;
        }

        private void Shatter()
        {
            if (pawn.Spawned) FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, ThingDefOf.Filth_Blood, 2);
            pawn.health.RemoveHediff(this);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            HardbloodGraphics.ForgetMask(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shellPoints, "shellPoints");
            Scribe_Values.Look(ref maximumShellPoints, "maximumShellPoints");
            Scribe_Values.Look(ref regenerationMultiplier, "regenerationMultiplier", 1f);
        }
    }
}
