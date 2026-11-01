using Bloodfiends.Bloodbags;
using RimWorld;
using Verse;

namespace Bloodfiends.Family
{
    public class CompProperties_AbilityEmbrace : CompProperties_AbilityEffect
    {
        public int outsiderGoodwillImpact = -30;          // embracing someone from another faction angers it
        public ThoughtDef thoughtDefToGiveTarget;         // how the victim feels about being bitten (vanilla bloodfeed's)
        public ThoughtDef opinionThoughtDefToGiveTarget;  // and about the biter
        public HistoryEventDef historyEvent;              // counts as bloodfeeding, for ideologies
        public float embracerHemogenFraction = 0.5f;      // making a Kindred costs the embracer this share of their full bar, on top of the cast cost
        public HediffDef embracerWeaknessHediff;          // and leaves them weakened for a while
        public ThoughtDef willingKindredThought;          // a Kindred who wanted it (already one of the embracer's people)
        public ThoughtDef unwillingKindredThought;        // a Kindred turned against their will (a prisoner or an outsider)
        public ThoughtDef unwillingKindredOpinionThought; // and what they think of the one who did it
        public int kindredComaTicks = 30000;              // a new Kindred lies in vanilla's xenogermination coma this long (half a day); 0 for none
        public CompProperties_AbilityEmbrace() => compClass = typeof(CompAbilityEffect_Embrace);

    }

    public class CompAbilityEffect_Embrace : CompAbilityEffect, IExtraHemogenCost
    {
        private enum EmbraceResult
        {
            Kindred,
            Bloodbag,
            Xenogenes
        }

        public new CompProperties_AbilityEmbrace Props => (CompProperties_AbilityEmbrace)props;

        private BloodfiendFamilyNode CasterNode => parent.pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node;

        private bool CasterIsBloodbag => BloodbagUtility.IsBloodbag(parent.pawn);

        private bool CasterIsCursed => parent.pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() != null;

        public float ExtraHemogenCost
        {
            get
            {
                if (Result == EmbraceResult.Bloodbag) return 0f;
                Gene_Hemogen hemogenGene = parent.pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
                return (hemogenGene?.Max ?? 0f) * Props.embracerHemogenFraction;
            }
        }

        private EmbraceResult Result
        {
            get
            {
                if (CasterIsBloodbag) return EmbraceResult.Bloodbag;
                if (!CasterIsCursed) return EmbraceResult.Xenogenes;
                return GameComponent_BloodfiendFamilies.Instance?.CanEmbraceKindred(CasterNode) == true ? EmbraceResult.Kindred : EmbraceResult.Bloodbag;
            }
        }

        /// <summary>
        /// The Family the caster's Bloodbags join: a Bloodfiend's own, or the one a Bloodbag belongs to.
        /// </summary>
        private int CasterFamilyId => CasterIsBloodbag ? BloodbagUtility.FamilyOf(parent.pawn) : CasterNode?.familyId ?? -1;

        public override bool GizmoDisabled(out string reason)
        {
            if (CasterIsCursed && !CasterIsBloodbag && (GameComponent_BloodfiendFamilies.Instance == null || CasterNode == null))
            {
                reason = "Bloodfiends_NotInFamily".Translate();
                return true;
            }
            EmbraceResult result = Result;
            if (result == EmbraceResult.Bloodbag && BloodbagUtility.BloodbagDef == null)
            {
                reason = ModsConfig.AnomalyActive ? "Bloodfiends_BloodbagsNotReady".Translate() : "Bloodfiends_BloodbagsNeedAnomaly".Translate();
                return true;
            }
            if (result == EmbraceResult.Xenogenes && parent.pawn.genes?.Xenogenes.NullOrEmpty() != false)
            {
                reason = "Bloodfiends_NoXenogenesToPass".Translate();
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        /// <summary>
        /// Says in the gizmo's tooltip what the next embrace does.
        /// </summary>
        public override string ExtraTooltipPart()
        {
            switch (Result)
            {
                case EmbraceResult.Kindred: return "Bloodfiends_EmbraceNextKindred".Translate(GameComponent_BloodfiendFamilies.MaximumKindredsPerBloodfiend - GameComponent_BloodfiendFamilies.Instance.KindredSlotsUsedBy(CasterNode));
                case EmbraceResult.Bloodbag: return "Bloodfiends_EmbraceNextBloodbag".Translate();
                default: return "Bloodfiends_EmbraceNextXenogenes".Translate();
            }
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
            if (!targetPawn.Downed && !targetPawn.IsPrisonerOfColony && targetPawn.Faction != parent.pawn.Faction && !IsSeeker(targetPawn)) return "Bloodfiends_EmbraceInvalidTarget".Translate();
            return null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn casterPawn = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null) return;

            Faction originalFaction = targetPawn.Faction;
            switch (Result)
            {
                case EmbraceResult.Kindred:
                    MakeKindred(casterPawn, targetPawn);
                    break;
                case EmbraceResult.Bloodbag:
                    BloodbagUtility.MakeBloodbag(targetPawn, casterPawn.Faction, casterPawn, CasterFamilyId);
                    Messages.Message("Bloodfiends_EmbracedBloodbag".Translate(casterPawn.Named("CASTER"), targetPawn.Named("BLOODBAG")), targetPawn, MessageTypeDefOf.NeutralEvent);
                    break;
                default:
                    PassOnXenogenes(casterPawn, targetPawn);
                    break;
            }
            bool seeker = IsSeeker(targetPawn);
            if (!seeker) AffectOutsider(casterPawn, targetPawn, originalFaction);
            if (seeker && targetPawn.Faction == casterPawn.Faction) RemoveHediff(targetPawn, BloodfiendsDefOf.Bloodfiends_SeekingEmbrace);
            if (Props.historyEvent != null) Find.HistoryEventsManager.RecordEvent(new HistoryEvent(Props.historyEvent, casterPawn.Named(HistoryEventArgsNames.Doer)));
        }

        private void MakeKindred(Pawn casterPawn, Pawn kindredPawn, bool joinsEmbracer = true)
        {
            BloodfiendFamilyNode kindredNode = GameComponent_BloodfiendFamilies.Instance?.CreateKindredNode(CasterNode, kindredPawn);
            if (kindredNode == null) return;
            bool willing = !joinsEmbracer || IsWilling(casterPawn, kindredPawn);

            kindredPawn.genes.SetXenotype(BloodfiendsDefOf.Bloodfiends_Bloodfiend);
            Hediff_BloodfiendLineage lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, kindredPawn);
            lineageHediff.nodeId = kindredNode.nodeId;
            kindredPawn.health.AddHediff(lineageHediff);

            TakeUnder(kindredPawn);
            DrainEmbracer(casterPawn);
            if (joinsEmbracer && kindredPawn.Faction != casterPawn.Faction) RecruitUtility.Recruit(kindredPawn, casterPawn.Faction, casterPawn);
            GiveTurningThoughts(casterPawn, kindredPawn, willing);

            FilthMaker.TryMakeFilth(kindredPawn.PositionHeld, kindredPawn.MapHeld, ThingDefOf.Filth_Blood, 3);
            Messages.Message("Bloodfiends_Embraced".Translate(casterPawn.Named("CASTER"), kindredPawn.Named("KINDRED")), kindredPawn, MessageTypeDefOf.PositiveEvent);
        }

        public bool CanMakeKindredNow => CasterNode != null && GameComponent_BloodfiendFamilies.Instance?.CanEmbraceKindred(CasterNode) == true;

        public void EmbraceForPlayer(Pawn colonist)
        {
            if (!CanMakeKindredNow) return;
            MakeKindred(parent.pawn, colonist, joinsEmbracer: false);
        }

        private void PassOnXenogenes(Pawn casterPawn, Pawn targetPawn)
        {
            bool willing = IsWilling(casterPawn, targetPawn);
            GeneUtility.ReimplantXenogerm(casterPawn, targetPawn);
            RemoveHediff(targetPawn, HediffDefOf.XenogerminationComa);
            RemoveHediff(casterPawn, HediffDefOf.XenogermReplicating);

            TakeUnder(targetPawn);
            DrainEmbracer(casterPawn);
            GiveTurningThoughts(casterPawn, targetPawn, willing);

            FilthMaker.TryMakeFilth(targetPawn.PositionHeld, targetPawn.MapHeld, ThingDefOf.Filth_Blood, 2);
            Messages.Message("Bloodfiends_EmbracedXenogenes".Translate(casterPawn.Named("CASTER"), targetPawn.Named("TARGET")), targetPawn, MessageTypeDefOf.NeutralEvent);
        }

        private static bool IsWilling(Pawn casterPawn, Pawn targetPawn) => IsSeeker(targetPawn) || targetPawn.Faction == casterPawn.Faction && !targetPawn.IsPrisoner;

        private static bool IsSeeker(Pawn pawn) => pawn.health.hediffSet.HasHediff(BloodfiendsDefOf.Bloodfiends_SeekingEmbrace);

        private void TakeUnder(Pawn targetPawn)
        {
            Gene_Hemogen targetHemogen = targetPawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (targetHemogen != null) targetHemogen.Value = 0f;
            if (Props.kindredComaTicks <= 0) return;
            Hediff coma = HediffMaker.MakeHediff(HediffDefOf.XenogerminationComa, targetPawn);
            targetPawn.health.AddHediff(coma);
            HediffComp_Disappears comaTimer = coma.TryGetComp<HediffComp_Disappears>();
            if (comaTimer != null) comaTimer.ticksToDisappear = Props.kindredComaTicks;
        }

        private void DrainEmbracer(Pawn casterPawn)
        {
            Gene_Hemogen casterHemogen = casterPawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (casterHemogen != null) casterHemogen.Value -= casterHemogen.Max * Props.embracerHemogenFraction;
            if (Props.embracerWeaknessHediff != null) casterPawn.health.AddHediff(Props.embracerWeaknessHediff);
        }

        private void GiveTurningThoughts(Pawn casterPawn, Pawn targetPawn, bool willing)
        {
            MemoryThoughtHandler memories = targetPawn.needs?.mood?.thoughts.memories;
            if (willing && Props.willingKindredThought != null) memories?.TryGainMemory(Props.willingKindredThought, casterPawn);
            if (willing) return;
            if (Props.unwillingKindredThought != null) memories?.TryGainMemory(Props.unwillingKindredThought, casterPawn);
            if (Props.unwillingKindredOpinionThought != null) memories?.TryGainMemory(Props.unwillingKindredOpinionThought, casterPawn);
        }

        private static void RemoveHediff(Pawn pawn, HediffDef hediffDef)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null) pawn.health.RemoveHediff(hediff);
        }

        private void AffectOutsider(Pawn casterPawn, Pawn targetPawn, Faction originalFaction)
        {
            if (originalFaction == null || originalFaction == casterPawn.Faction) return;

            if (!originalFaction.IsPlayer && casterPawn.Faction != null && Props.outsiderGoodwillImpact != 0) originalFaction.TryAffectGoodwillWith(casterPawn.Faction, Props.outsiderGoodwillImpact, true, true, null, targetPawn);

            // Bloodbags have no mood to be upset with
            if (targetPawn.needs?.mood == null) return;
            if (Props.thoughtDefToGiveTarget != null) targetPawn.needs.mood.thoughts.memories.TryGainMemory(Props.thoughtDefToGiveTarget);
            if (Props.opinionThoughtDefToGiveTarget != null) targetPawn.needs.mood.thoughts.memories.TryGainMemory(Props.opinionThoughtDefToGiveTarget, casterPawn);
        }
    }
}