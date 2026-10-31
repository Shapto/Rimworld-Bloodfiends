using Bloodfiends.Family;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;

namespace Bloodfiends.Quests
{
    public class QuestPart_FamilyGathering : QuestPart_SagnuophageMeeting
    {
        protected override Lord MakeLord()
        {
            Lord lord = LordMaker.MakeNewLord(faction, new LordJob_FamilyGathering(targetCell, enemyTargets, meetingDurationTicks, outSignalMeetingStarted, outSignalMeetingCompleted, outSignalAllSanguophagesGone, inSignalDefend, inSignalQuestSuccess, inSignalQuestFail), Map);
            QuestUtility.AddQuestTag(ref lord.questTags, questTag);
            return lord;
        }
    }

    public class LordJob_FamilyGathering : LordJob_SanguophageMeeting
    {
        private const float LeaveAtExposureSeverity = 0.35f;

        public LordJob_FamilyGathering()
        {
        }

        public LordJob_FamilyGathering(IntVec3 targetCell, List<Thing> enemyTargets, int meetingDurationTicks, string outSignalMeetingStarted, string outSignalMeetingEnded, string outSignalAllGone, string inSignalDefend, string inSignalQuestSuccess, string inSignalQuestFail) : base(targetCell, enemyTargets, meetingDurationTicks, outSignalMeetingStarted, outSignalMeetingEnded, outSignalAllGone, inSignalDefend, inSignalQuestSuccess, inSignalQuestFail)
        {
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var travel = new LordToil_Travel(targetCell);
            graph.AddToil(travel);
            graph.StartingToil = travel;
            var meeting = new LordToil_FamilyGathering(targetCell, meetingDurationTicks);
            graph.AddToil(meeting);
            var exitMap = new LordToil_ExitMapRandom();
            graph.AddToil(exitMap);
            var exitAndDefend = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(exitAndDefend);
            var assault = new LordToil_AssaultThings(enemyTargets);
            graph.AddToil(assault);

            var arrive = new Transition(travel, meeting);
            arrive.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrive.AddPostAction(new TransitionAction_Custom(() =>
            {
                if (!outSignalMeetingStarted.NullOrEmpty()) Find.SignalManager.SendSignal(new Signal(outSignalMeetingStarted));
                var torch = (Building)ThingMaker.MakeThing(ThingDefOf.SanguphageMeetingTorch);
                torch.SetFaction(lord.faction);
                if (GenPlace.TryPlaceThing(torch, targetCell, Map, ThingPlaceMode.Near)) lord.AddBuilding(torch);
            }));
            graph.AddTransition(arrive);

            var finish = new Transition(meeting, exitMap);
            finish.AddTrigger(new Trigger_TicksPassed(meetingDurationTicks));
            finish.AddTrigger(new Trigger_Signal(inSignalQuestSuccess));
            finish.AddTrigger(new Trigger_Signal(inSignalQuestFail));
            finish.AddPostAction(new TransitionAction_Custom(() => Find.SignalManager.SendSignal(new Signal(outSignalMeetingEnded))));
            graph.AddTransition(finish);

            var turnedOn = new Transition(meeting, exitAndDefend);
            turnedOn.AddSource(travel);
            turnedOn.AddSource(exitMap);
            turnedOn.AddSource(assault);
            turnedOn.AddTrigger(new Trigger_BecamePlayerEnemy());
            turnedOn.AddTrigger(new Trigger_PawnKilled());
            turnedOn.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(turnedOn);

            var harmed = new Transition(travel, exitAndDefend);
            harmed.AddSource(meeting);
            harmed.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: true, Faction.OfPlayer));
            harmed.AddPostAction(new TransitionAction_Custom(() => QuestUtility.SendQuestTargetSignals(lord.questTags, "BeingAttacked", lord.Named("SUBJECT"))));
            graph.AddTransition(harmed);

            var defend = new Transition(meeting, assault);
            defend.AddTrigger(new Trigger_Signal(inSignalDefend));
            defend.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(defend);

            var defended = new Transition(assault, exitMap);
            defended.AddTrigger(new Trigger_Signal(inSignalQuestSuccess));
            graph.AddTransition(defended);

            var weather = new Transition(meeting, exitAndDefend);
            weather.AddTrigger(new Trigger_Custom(signal => signal.type == TriggerSignalType.Tick && lord.ownedPawns.Any(SufferingExposure)));
            weather.AddPostAction(new TransitionAction_Custom(() =>
            {
                Messages.Message("Bloodfiends_GatheringLeavingTemperature".Translate(), lord.ownedPawns, MessageTypeDefOf.NegativeEvent);
                QuestUtility.SendQuestTargetSignals(lord.questTags, "BeingAttacked", lord.Named("SUBJECT"));
            }));
            graph.AddTransition(weather);
            return graph;
        }

        private static bool SufferingExposure(Pawn pawn)
        {
            if (pawn.Dead) return false;
            Hediff hypothermia = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
            Hediff heatstroke = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
            return hypothermia != null && hypothermia.Severity >= LeaveAtExposureSeverity || heatstroke != null && heatstroke.Severity >= LeaveAtExposureSeverity;
        }
    }

    public class LordToil_FamilyGathering : LordToil_SanguophageMeeting
    {
        public LordToil_FamilyGathering(IntVec3 meetingSpot, int meetingDurationTicks) : base(meetingSpot, meetingDurationTicks)
        {
        }

        public override void Init() => Messages.Message("Bloodfiends_GatheringBegun".Translate(), lord.ownedPawns, MessageTypeDefOf.NeutralEvent);
    }

    [StaticConstructorOnStartup]
    public class Reward_FamilyEmbrace : Reward
    {
        private static readonly CachedTexture Icon = new CachedTexture("Icons/Embrace");

        public override IEnumerable<GenUI.AnonymousStackElement> StackElements
        {
            get
            {
                yield return QuestPartUtility.GetStandardRewardStackElement("Bloodfiends_RewardFamilyEmbraceLabel".Translate(), Icon.Texture, () => GetDescription(default(RewardsGeneratorParams)).CapitalizeFirst());
            }
        }

        public override void InitFromValue(float rewardValue, RewardsGeneratorParams parms, out float valueActuallyUsed) => throw new System.NotImplementedException();

        public override IEnumerable<QuestPart> GenerateQuestParts(int index, RewardsGeneratorParams parms, string customLetterLabel, string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules)
        {
            yield break;
        }

        public override string GetDescription(RewardsGeneratorParams parms) => "Bloodfiends_RewardFamilyEmbraceDesc".Translate().Resolve();
    }

    public class QuestPart_FamilyEmbrace : QuestPart_MakeLord
    {
        public IntVec3 gatherSpot;
        public string inSignalEmbraced;
        public int waitDurationTicks;

        protected override Lord MakeLord() => LordMaker.MakeNewLord(faction, new LordJob_FamilyEmbrace(gatherSpot, waitDurationTicks, inSignalEmbraced), Map);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref gatherSpot, "gatherSpot");
            Scribe_Values.Look(ref inSignalEmbraced, "inSignalEmbraced");
            Scribe_Values.Look(ref waitDurationTicks, "waitDurationTicks");
        }
    }
    public class LordJob_FamilyEmbrace : LordJob
    {
        private IntVec3 idleSpot;
        public string inSignalEmbraced;
        private int waitDurationTicks;

        public LordJob_FamilyEmbrace()
        {
        }

        public LordJob_FamilyEmbrace(IntVec3 idleSpot, int waitDurationTicks, string inSignalEmbraced)
        {
            this.idleSpot = idleSpot;
            this.waitDurationTicks = waitDurationTicks;
            this.inSignalEmbraced = inSignalEmbraced;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var travel = new LordToil_Travel(idleSpot);
            graph.AddToil(travel);
            graph.StartingToil = travel;
            var waitToEmbrace = new LordToil_FamilyEmbrace(idleSpot);
            graph.AddToil(waitToEmbrace);
            var exitMap = new LordToil_ExitMapRandom();
            graph.AddToil(exitMap);
            var exitAndDefend = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(exitAndDefend);

            var arrived = new Transition(travel, waitToEmbrace);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.canMoveToSameState = true;
            graph.AddTransition(arrived);

            var done = new Transition(waitToEmbrace, exitMap);
            done.AddSource(travel);
            done.AddTrigger(new Trigger_Signal(inSignalEmbraced));
            done.AddTrigger(new Trigger_TicksPassed(waitDurationTicks));
            done.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(done);

            var turnedOn = new Transition(waitToEmbrace, exitAndDefend);
            turnedOn.AddSource(travel);
            turnedOn.AddSource(exitMap);
            turnedOn.AddTrigger(new Trigger_BecamePlayerEnemy());
            turnedOn.AddTrigger(new Trigger_PawnKilled());
            turnedOn.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(turnedOn);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref idleSpot, "idleSpot");
            Scribe_Values.Look(ref inSignalEmbraced, "inSignalEmbraced");
            Scribe_Values.Look(ref waitDurationTicks, "waitDurationTicks");
        }
    }

    public class LordToil_FamilyEmbrace : LordToil
    {
        private const float WanderRadius = 10f;

        public override bool ShouldFail => Data.target == null;

        public LordToilData_ReimplantXenogerm Data => (LordToilData_ReimplantXenogerm)data;

        public LordToil_FamilyEmbrace(IntVec3 spot)
        {
            data = new LordToilData_ReimplantXenogerm { waitSpot = spot };
        }

        public static bool IsWaitingToEmbrace(Pawn pawn) => pawn.GetLord()?.CurLordToil is LordToil_FamilyEmbrace toil && toil.Data.target == pawn;

        public static void Notify_Embraced(Pawn embracer)
        {
            if (embracer.GetLord()?.LordJob is LordJob_FamilyEmbrace job && !job.inSignalEmbraced.NullOrEmpty()) Find.SignalManager.SendSignal(new Signal(job.inSignalEmbraced));
        }

        public override void Init() => SetTarget();

        private void SetTarget()
        {
            Data.target = lord.ownedPawns
                .Where(pawn => !pawn.Destroyed && !pawn.Downed && ForcedEmbrace.EmbraceOf(pawn)?.CanMakeKindredNow == true)
                .RandomElementWithFallback();
            if (Data.target == null) return;
            Find.LetterStack.ReceiveLetter("Bloodfiends_LetterLabelWaitingToEmbrace".Translate(), "Bloodfiends_LetterWaitingToEmbrace".Translate(Data.target.Named("PAWN"), 15000.ToStringTicksToPeriod().Named("DURATION")), LetterDefOf.PositiveEvent, Data.target);
        }

        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = pawn == Data.target ? new PawnDuty(DutyDefOf.Idle, Data.waitSpot) : new PawnDuty(DutyDefOf.WanderClose_NoNeeds, Data.waitSpot, WanderRadius);
            }
        }

        public override void DrawPawnGUIOverlay(Pawn pawn)
        {
            if (pawn == Data.target) pawn.Map.overlayDrawer.DrawOverlay(pawn, OverlayTypes.QuestionMark);
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            if (pawn == Data.target) SetTarget();
        }
    }
}