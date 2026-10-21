using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class CompProperties_AbilityEmbraceKindred : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityEmbraceKindred() => compClass = typeof(CompAbilityEffect_EmbraceKindred);
    }

    /// <summary>
    /// Embrace: Kindred. Turns a human into a Bloodfiend one generation below the caster, in the caster's Family.
    /// The hemogen cost is handled by vanilla's hemogen cost comp on the same ability.
    /// </summary>
    public class CompAbilityEffect_EmbraceKindred : CompAbilityEffect
    {
        private BloodfiendFamilyNode CasterNode => parent.pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node;

        public override bool GizmoDisabled(out string reason)
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode casterNode = CasterNode;

            if (registry == null || casterNode == null)
            {
                reason = "Not part of a Bloodfiend Family.";
                return true;
            }
            if (!registry.CanEmbraceKindred(casterNode))
            {
                reason = "No Kindred slots left.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            string rejectionReason = GetRejectionReason(target.Pawn);
            if (rejectionReason == null) return true;

            if (throwMessages) Messages.Message(rejectionReason, target.Pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        private string GetRejectionReason(Pawn targetPawn)
        {
            if (targetPawn == null || targetPawn.Dead || !targetPawn.RaceProps.Humanlike) return "Only living humans can be embraced.";
            if (targetPawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() != null) return "Already a Bloodfiend.";
            if (!targetPawn.Downed && !targetPawn.IsPrisonerOfColony && targetPawn.Faction != parent.pawn.Faction) return "Can only embrace someone who is downed, a prisoner, or one of your own.";
            return null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn casterPawn = parent.pawn;
            Pawn kindredPawn = target.Pawn;
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null || kindredPawn == null) return;

            BloodfiendFamilyNode kindredNode = registry.CreateKindredNode(CasterNode, kindredPawn);
            if (kindredNode == null) return;

            // The hediff goes on before the xenotype, so the Bloodfiend gene sees it and doesn't roll a random tier.
            Hediff_BloodfiendLineage lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, kindredPawn);
            lineageHediff.nodeId = kindredNode.nodeId;
            kindredPawn.health.AddHediff(lineageHediff);
            kindredPawn.genes.SetXenotype(BloodfiendsDefOf.Bloodfiends_Bloodfiend);

            if (kindredPawn.Faction != casterPawn.Faction) RecruitUtility.Recruit(kindredPawn, casterPawn.Faction, casterPawn);

            FilthMaker.TryMakeFilth(kindredPawn.PositionHeld, kindredPawn.MapHeld, ThingDefOf.Filth_Blood, 3);
            Messages.Message($"{casterPawn.LabelShort} has embraced {kindredPawn.LabelShort}.", kindredPawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}
