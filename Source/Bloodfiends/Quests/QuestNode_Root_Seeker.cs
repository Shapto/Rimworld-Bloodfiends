using Bloodfiends.Family;
using RimWorld;
using RimWorld.QuestGen;
using System.Linq;
using Verse;

namespace Bloodfiends.Quests
{
    public class QuestNode_Root_Seeker : QuestNode
    {
        private const int WaitTicks = 180000;              // 3 days
        private const int GoodwillLossIfIgnored = -10;
        private static readonly IntRange ArrivalDelayTicks = new IntRange(2500, 7500);

        protected override bool TestRunInt(Slate slate)
        {
            Map map = QuestGen_Get.GetMap();
            return map != null && GameComponent_BloodfiendFamilies.Instance?.PlayerCanEmbraceKindred() == true && TryFindFaction(out _) && TryFindWaitSpot(map, out _);
        }

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();
            if (map == null || !TryFindFaction(out Faction faction) || !TryFindWaitSpot(map, out IntVec3 waitSpot)) return;
            slate.Set("map", map);
            slate.Set("faction", faction);
            slate.Set("stayDuration", WaitTicks.ToStringTicksToPeriod());

            var request = new PawnGenerationRequest(faction.def.basicMemberKind, faction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, validatorPostGear: pawn => pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() == null && !pawn.IsMutant);
            Pawn seeker = quest.GeneratePawn(request);
            seeker.health.AddHediff(BloodfiendsDefOf.Bloodfiends_SeekingEmbrace);
            slate.Set("seeker", seeker);

            string embracedSignal = QuestGenUtility.HardcodedSignalWithQuestID("seeker.ChangedFactionToPlayer");
            string leftSignal = QuestGenUtility.HardcodedSignalWithQuestID("seeker.LeftMap");
            string killedSignal = QuestGenUtility.HardcodedSignalWithQuestID("seeker.Killed");

            quest.Delay(ArrivalDelayTicks.RandomInRange, () =>
            {
                quest.PawnsArrive(Gen.YieldSingle(seeker), null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn, customLetterLabel: "[seekerArrivedLetterLabel]", customLetterText: "[seekerArrivedLetterText]");
                quest.WaitForDuration(map.Parent, Gen.YieldSingle(seeker), faction, waitSpot, WaitTicks);
            });

            quest.Letter(LetterDefOf.PositiveEvent, embracedSignal, lookTargets: Gen.YieldSingle(seeker), text: "[embracedLetterText]", label: "[embracedLetterLabel]");
            quest.End(QuestEndOutcome.Success, inSignal: embracedSignal);
            quest.FactionGoodwillChange(faction, GoodwillLossIfIgnored, leftSignal);
            quest.End(QuestEndOutcome.Fail, inSignal: leftSignal, sendStandardLetter: true);
            quest.End(QuestEndOutcome.Fail, inSignal: killedSignal);
        }

        private static bool TryFindFaction(out Faction faction) => Find.FactionManager.AllFactionsVisible
            .Where(candidate => !candidate.IsPlayer && !candidate.defeated && !candidate.temporary && candidate.def.humanlikeFaction && candidate.def.basicMemberKind != null)
            .Where(candidate => !candidate.HostileTo(Faction.OfPlayer))
            .TryRandomElement(out faction);

        private static bool TryFindWaitSpot(Map map, out IntVec3 waitSpot) => RCellFinder.TryFindRandomSpotJustOutsideColony(CellFinder.RandomEdgeCell(map), map, out waitSpot);
    }
}