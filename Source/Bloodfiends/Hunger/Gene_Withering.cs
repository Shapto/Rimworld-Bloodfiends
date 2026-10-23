using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hunger
{
    public class WitheringExtension : DefModExtension
    {
        public HediffDef witheringHediff;
        public float starvingBelow = 0.05f;           // hemogen share at or below which they wither
        public float recoveringAbove = 0.25f;         // hemogen share at or above which they recover
        public float severityPerDayStarving = 0.5f;
        public float severityPerDayRecovering = 2f;
        public MentalStateDef frenzyState;
        public float frenzyAtSeverity = 0.85f;   // ravenous: the last stage before Withering kills them 
        public float frenzyEndsAbove = 0.25f;   // hemogen share that brings them out of it
    }

    public class Gene_Withering : Gene
    {
        private const int UpdateIntervalTicks = 60;
        private static readonly WitheringExtension DefaultSettings = new WitheringExtension();

        public WitheringExtension Settings => def.GetModExtension<WitheringExtension>() ?? DefaultSettings;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn.Dead || !pawn.IsHashIntervalTick(UpdateIntervalTicks, delta)) return;

            Gene_Hemogen hemogenGene = pawn.genes.GetFirstGeneOfType<Gene_Hemogen>();
            HediffDef witheringDef = Settings.witheringHediff;
            if (hemogenGene == null || hemogenGene.Max <= 0f || witheringDef == null) return;

            float fullness = hemogenGene.Value / hemogenGene.Max;
            float severityPerDay = fullness <= Settings.starvingBelow ? Settings.severityPerDayStarving : fullness >= Settings.recoveringAbove ? -Settings.severityPerDayRecovering : 0f;
            if (severityPerDay == 0f) return;
            float change = severityPerDay * UpdateIntervalTicks / GenDate.TicksPerDay;

            Hediff withering = pawn.health.hediffSet.GetFirstHediffOfDef(witheringDef);
            if (withering == null)
            {
                if (change <= 0f) return;
                withering = pawn.health.AddHediff(witheringDef);
                withering.Severity = Mathf.Max(change, 0.01f);
                return;
            }
            withering.Severity += change;
            if (change > 0f) TryFrenzy(withering);
            if (change < 0f && withering.Severity <= 0.001f) pawn.health.RemoveHediff(withering);
        }

        private void TryFrenzy(Hediff withering)
        {
            if (Settings.frenzyState == null || withering.Severity < Settings.frenzyAtSeverity) return;
            if (!pawn.Spawned || pawn.Downed || pawn.InMentalState) return;
            pawn.mindState.mentalStateHandler.TryStartMentalState(Settings.frenzyState, null, forced: true, forceWake: true);
        }
    }
}
