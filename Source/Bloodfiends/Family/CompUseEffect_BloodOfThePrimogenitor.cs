using Bloodfiends.Bloodbags;
using RimWorld;
using Verse;

namespace Bloodfiends.Family
{
    public class CompUseEffect_BloodOfThePrimogenitor : CompUseEffect
    {
        private const int FounderComaTicks = 60000;

        public override AcceptanceReport CanBeUsedBy(Pawn pawn)
        {
            if (pawn.genes == null || !pawn.RaceProps.Humanlike) return false;
            if (pawn.genes.GetFirstGeneOfType<Gene_Bloodfiend>() != null) return "Bloodfiends_PrimogenitorAlreadyCursed".Translate(pawn.Named("PAWN"));
            if (pawn.IsMutant || BloodbagUtility.IsBloodbag(pawn)) return "Bloodfiends_PrimogenitorCannotTurn".Translate(pawn.Named("PAWN"));
            return base.CanBeUsedBy(pawn);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null) return;

            BloodfiendFamilyNode founderNode = registry.CreateFamilyFor(usedBy, GameComponent_BloodfiendFamilies.ElderGeneration);
            usedBy.genes.SetXenotype(BloodfiendsDefOf.Bloodfiends_Bloodfiend);

            Hediff_BloodfiendLineage lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, usedBy);
            lineageHediff.nodeId = founderNode.nodeId;
            usedBy.health.AddHediff(lineageHediff);

            Gene_Hemogen hemogenGene = usedBy.genes.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene != null) hemogenGene.Value = 0f;

            Hediff coma = HediffMaker.MakeHediff(HediffDefOf.XenogerminationComa, usedBy);
            usedBy.health.AddHediff(coma);
            HediffComp_Disappears comaTimer = coma.TryGetComp<HediffComp_Disappears>();
            if (comaTimer != null) comaTimer.ticksToDisappear = FounderComaTicks;

            if (usedBy.MapHeld != null) FilthMaker.TryMakeFilth(usedBy.PositionHeld, usedBy.MapHeld, ThingDefOf.Filth_Blood, 3);
            Find.LetterStack.ReceiveLetter("Bloodfiends_PrimogenitorDrankLabel".Translate(usedBy.Named("PAWN")), "Bloodfiends_PrimogenitorDrankText".Translate(usedBy.Named("PAWN")), LetterDefOf.PositiveEvent, usedBy);
        }
    }
}