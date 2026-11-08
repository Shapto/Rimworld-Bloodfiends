using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Scenarios
{
    public class ScenPart_BloodfiendStartingTier : ScenPart
    {
        public int generation = 1;
        public override void Notify_PawnGenerated(Pawn pawn, PawnGenerationContext context, bool redressed)
        {
            if (context != PawnGenerationContext.PlayerStarter || pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() == null) return;
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null) return;

            BloodfiendFamilyNode node = registry.RefoundFamily(pawn, generation);
            Hediff_BloodfiendLineage lineageHediff = pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>();
            if (lineageHediff == null)
            {
                lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, pawn);
                lineageHediff.nodeId = node.nodeId;
                pawn.health.AddHediff(lineageHediff);
                return;
            }
            lineageHediff.nodeId = node.nodeId;
            lineageHediff.SyncFromNode();
        }

        public override string Summary(Scenario scenario) => "Bloodfiends_ScenarioStartingTier".Translate(generation);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref generation, "generation", 1);
        }
    }
}
