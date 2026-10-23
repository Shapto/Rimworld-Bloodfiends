using Bloodfiends.Bloodbags;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class CompProperties_AbilityEmbrace : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityEmbrace() => compClass = typeof(CompAbilityEffect_Embrace);
    }

    public class CompAbilityEffect_Embrace : CompAbilityEffect
    {
        private BloodfiendFamilyNode CasterNode => parent.pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node;

        private bool CasterIsBloodbag => BloodbagUtility.IsBloodbag(parent.pawn);

        private bool MakesKindred => !CasterIsBloodbag && GameComponent_BloodfiendFamilies.Instance?.CanEmbraceKindred(CasterNode) == true;

        /// <summary>
        /// The Family the caster's Bloodbags join: a Bloodfiend's own, or the one a Bloodbag belongs to.
        /// </summary>
        private int CasterFamilyId => CasterIsBloodbag ? BloodbagUtility.FamilyOf(parent.pawn) : CasterNode?.familyId ?? -1;

        public override bool GizmoDisabled(out string reason)
        {
            if (!CasterIsBloodbag && (GameComponent_BloodfiendFamilies.Instance == null || CasterNode == null))
            {
                reason = "Bloodfiends_NotInFamily".Translate();
                return true;
            }
            if (!MakesKindred && BloodbagUtility.BloodbagDef == null)
            {
                reason = ModsConfig.AnomalyActive ? "Bloodfiends_BloodbagsNotReady".Translate() : "Bloodfiends_BloodbagsNeedAnomaly".Translate();
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        /// <summary>
        /// Says in the gizmo's tooltip what the next embrace makes.
        /// </summary>
        public override string ExtraTooltipPart()
        {
            if (!MakesKindred) return "Bloodfiends_EmbraceNextBloodbag".Translate();
            return "Bloodfiends_EmbraceNextKindred".Translate(GameComponent_BloodfiendFamilies.MaximumKindredsPerBloodfiend - CasterNode.kindredCreatedCount);
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
            if (targetPawn == null || targetPawn.Dead || !targetPawn.RaceProps.Humanlike) return "Bloodfiends_EmbraceOnlyLivingHumans".Translate();
            if (targetPawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() != null) return "Bloodfiends_AlreadyBloodfiend".Translate();
            if (targetPawn.IsMutant) return "Bloodfiends_AlreadyMutant".Translate();
            if (!targetPawn.Downed && !targetPawn.IsPrisonerOfColony && targetPawn.Faction != parent.pawn.Faction) return "Bloodfiends_EmbraceInvalidTarget".Translate();
            return null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn casterPawn = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null) return;

            if (MakesKindred)
            {
                MakeKindred(casterPawn, targetPawn);
                return;
            }
            BloodbagUtility.MakeBloodbag(targetPawn, casterPawn.Faction, casterPawn, CasterFamilyId);
            Messages.Message("Bloodfiends_EmbracedBloodbag".Translate(casterPawn.Named("CASTER"), targetPawn.Named("BLOODBAG")), targetPawn, MessageTypeDefOf.NeutralEvent);
        }

        private void MakeKindred(Pawn casterPawn, Pawn kindredPawn)
        {
            BloodfiendFamilyNode kindredNode = GameComponent_BloodfiendFamilies.Instance?.CreateKindredNode(CasterNode, kindredPawn);
            if (kindredNode == null) return;

            Hediff_BloodfiendLineage lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, kindredPawn);
            lineageHediff.nodeId = kindredNode.nodeId;
            kindredPawn.health.AddHediff(lineageHediff);
            kindredPawn.genes.SetXenotype(BloodfiendsDefOf.Bloodfiends_Bloodfiend);

            if (kindredPawn.Faction != casterPawn.Faction) RecruitUtility.Recruit(kindredPawn, casterPawn.Faction, casterPawn);

            FilthMaker.TryMakeFilth(kindredPawn.PositionHeld, kindredPawn.MapHeld, ThingDefOf.Filth_Blood, 3);
            Messages.Message("Bloodfiends_Embraced".Translate(casterPawn.Named("CASTER"), kindredPawn.Named("KINDRED")), kindredPawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}
