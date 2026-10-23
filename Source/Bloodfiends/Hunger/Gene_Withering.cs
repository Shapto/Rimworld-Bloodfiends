using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }

    public class Gene_Withering : Gene
    {
        private const int UpdateIntervalTicks = 60;
        private static readonly WitheringExtension DefaultSettings = new WitheringExtension();

        private WitheringExtension Settings => def.GetModExtension<WitheringExtension>() ?? DefaultSettings;

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
                withering.Severity = change;
                return;
            }
            withering.Severity += change;
            if (withering.Severity <= 0.001f) pawn.health.RemoveHediff(withering);
        }
    }
}
