using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Bloodfiends.Family
{
    public static class ForcedEmbrace
    {
        public static CompAbilityEffect_Embrace EmbraceOf(Pawn bloodfiend) => bloodfiend?.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_Embrace)?.CompOfType<CompAbilityEffect_Embrace>();

        public static bool IsCaptive(Pawn pawn) => pawn.IsPrisonerOfColony || pawn.Downed && pawn.Faction != Faction.OfPlayer;

        public static bool CanOfferEmbrace(Pawn pawn) => IsCaptive(pawn) || Bloodfiends.Quests.LordToil_FamilyEmbrace.IsWaitingToEmbrace(pawn);

        public static string CannotReason(Pawn captive, Pawn colonist, CompAbilityEffect_Embrace embrace)
        {
            if (colonist.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() != null) return "Bloodfiends_AlreadyBloodfiend".Translate();
            if (colonist.IsMutant) return "Bloodfiends_AlreadyMutant".Translate();
            if (colonist.IsQuestLodger()) return "TemporaryFactionMember".Translate(colonist.Named("PAWN"));
            if (!embrace.CanMakeKindredNow) return "Bloodfiends_CaptiveNoKindredSlots".Translate(captive.Named("EMBRACER"));
            return null;
        }
    }

    public class FloatMenuOptionProvider_ForcedEmbrace : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn colonist = context.FirstSelectedPawn;
            if (!colonist.IsColonistPlayerControlled || !colonist.RaceProps.Humanlike || !ForcedEmbrace.CanOfferEmbrace(clickedPawn)) return null;
            CompAbilityEffect_Embrace embrace = ForcedEmbrace.EmbraceOf(clickedPawn);
            if (embrace == null || clickedPawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() == null) return null;

            string label = "Bloodfiends_ReceiveEmbrace".Translate(clickedPawn.Named("EMBRACER"));
            string reason = ForcedEmbrace.CannotReason(clickedPawn, colonist, embrace);
            if (reason != null) return new FloatMenuOption(label + ": " + reason, null);
            if (!colonist.CanReserveAndReach(clickedPawn, PathEndMode.Touch, Danger.Deadly)) return new FloatMenuOption(label + ": " + "NoPath".Translate().CapitalizeFirst(), null);
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () => colonist.jobs.TryTakeOrderedJob(JobMaker.MakeJob(BloodfiendsDefOf.Bloodfiends_ReceiveEmbrace, clickedPawn), JobTag.Misc)), colonist, clickedPawn);
        }
    }

    public class JobDriver_ReceiveEmbrace : JobDriver
    {
        private const int BiteTicks = 180;

        private Pawn Embracer => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(Embracer, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !ForcedEmbrace.CanOfferEmbrace(Embracer) || ForcedEmbrace.EmbraceOf(Embracer) == null || ForcedEmbrace.CannotReason(Embracer, pawn, ForcedEmbrace.EmbraceOf(Embracer)) != null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil bite = Toils_General.Wait(BiteTicks, TargetIndex.A);
            bite.AddPreInitAction(() => SoundDefOf.Bloodfeed_Cast.PlayOneShot(new TargetInfo(Embracer.Position, Embracer.Map)));
            bite.WithEffect(() => DefDatabase<EffecterDef>.GetNamedSilentFail("Bloodfeed_Warmup"), TargetIndex.A);
            bite.WithProgressBarToilDelay(TargetIndex.A);
            yield return bite;

            yield return Toils_General.Do(() =>
            {
                Pawn embracer = Embracer;
                ForcedEmbrace.EmbraceOf(embracer)?.EmbraceForPlayer(pawn);
                Bloodfiends.Quests.LordToil_FamilyEmbrace.Notify_Embraced(embracer);
            });
        }
    }
}