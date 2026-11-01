using Bloodfiends.Family;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Bloodfiends.Quests
{
    /// <summary>
    /// A Bloodfiend on the run asks to join. Accepting brings them in, and their hunter follows with thralls.
    /// The hunter can be any tier and any Family. If the hunter is above the runaway in the tree, killing them moves the runaway up a generation.
    /// </summary>
    public class QuestNode_Root_HuntedKindred : QuestNode
    {
        private const float HunterCombatPoints = 250f;
        private const int MaximumGuardCount = 6;
        private const float EmbracedByHunterChance = 0.5f;

        private static readonly IntRange JoinerArrivalDelayRange = new IntRange(600, 1200);
        private static readonly IntRange HunterArrivalDelayRange = new IntRange(1800, 2400);

        protected override bool TestRunInt(Slate slate)
        {
            Map map = QuestGen_Get.GetMap();
            return GameComponent_BloodfiendFamilies.Instance != null && map != null && RCellFinder.TryFindRandomPawnEntryCell(out _, map, CellFinder.EdgeRoadChance_Hostile);
        }

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            Map map = QuestGen_Get.GetMap();
            float points = slate.Get("points", 0f);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 walkInSpot, map, CellFinder.EdgeRoadChance_Hostile)) return;

            Faction hunterFaction = BloodfiendQuestUtility.MakeHostileTemporaryFaction();
            Pawn hunter = BloodfiendQuestUtility.GenerateBloodfiend(PawnKindDefOf.Sanguophage, hunterFaction, map.Tile);
            BloodfiendFamilyNode hunterNode = registry.GetNodeForPawn(hunter);

            int joinerEmbracerNodeId = hunterNode != null && Rand.Chance(EmbracedByHunterChance) ? hunterNode.nodeId : -1;
            Pawn joiner = BloodfiendQuestUtility.GenerateBloodfiend(PawnKindDefOf.Villager, null, map.Tile, forceRecruitable: true, embracingNodeId: joinerEmbracerNodeId);
            BloodfiendFamilyNode joinerNode = registry.GetNodeForPawn(joiner);

            List<Pawn> guards = BloodfiendQuestUtility.GenerateGuards(hunterFaction, map.Tile, hunter, points - HunterCombatPoints, MaximumGuardCount);
            List<Pawn> hunters = new List<Pawn> { hunter };
            hunters.AddRange(guards);
            foreach (Pawn generatedPawn in hunters.Append(joiner))
                if (!generatedPawn.IsWorldPawn()) Find.WorldPawns.PassToWorld(generatedPawn);

            slate.Set("map", map);
            slate.Set("joiner", joiner);
            slate.Set("hunter", hunter);
            slate.Set("joinerTier", BloodfiendQuestUtility.TierLabelOf(joiner));
            slate.Set("hunterTier", BloodfiendQuestUtility.TierLabelOf(hunter));
            slate.Set("hunterRelation", HunterRelationTo(hunterNode, joinerNode));
            slate.Set("guardCount", guards.Count);

            quest.ReserveFaction(hunterFaction);
            quest.AddPart(new QuestPart_DiscardUnarrivedPawns { pawns = hunters.Append(joiner).ToList() });

            quest.Delay(JoinerArrivalDelayRange.RandomInRange, delegate
            {
                quest.PawnsArrive(Gen.YieldSingle(joiner), mapParent: map.Parent, joinPlayer: true, walkInSpot: walkInSpot, customLetterLabel: "Bloodfiends_HuntedKindredArrivedLabel".Translate(joiner.Named("JOINER")), customLetterText: "Bloodfiends_HuntedKindredArrivedText".Translate(joiner.Named("JOINER"), hunter.Named("HUNTER")));
            });

            quest.Delay(HunterArrivalDelayRange.RandomInRange, delegate
            {
                quest.PawnsArrive(hunters, mapParent: map.Parent, arrivalMode: PawnsArrivalModeDefOf.EdgeWalkIn, walkInSpot: walkInSpot, sendStandardLetter: false);
                quest.AssaultColony(hunterFaction, map.Parent, hunters);
                quest.Letter(LetterDefOf.ThreatBig, lookTargets: hunters, text: "Bloodfiends_HuntedKindredHunterArrivedText".Translate(joiner.Named("JOINER"), hunter.Named("HUNTER")), label: "Bloodfiends_HuntedKindredHunterArrivedLabel".Translate(hunter.Named("HUNTER")));
            });

            quest.End(QuestEndOutcome.Success, inSignal: QuestGenUtility.HardcodedSignalWithQuestID("hunter.Killed"));
            AddEnding(quest, "hunter.ChangedHostFaction", QuestEndOutcome.Success, LetterDefOf.PositiveEvent, "Bloodfiends_HuntedKindredCaptured", joiner, hunter);
            AddEnding(quest, "hunter.LeftMap", QuestEndOutcome.Success, LetterDefOf.PositiveEvent, "Bloodfiends_HuntedKindredRepelled", joiner, hunter);
            AddEnding(quest, "joiner.Killed", QuestEndOutcome.Fail, LetterDefOf.NegativeEvent, "Bloodfiends_HuntedKindredCaught", joiner, hunter);
        }

        private static string HunterRelationTo(BloodfiendFamilyNode hunterNode, BloodfiendFamilyNode joinerNode)
        {
            if (hunterNode == null || joinerNode == null || hunterNode.familyId != joinerNode.familyId) return "stranger";
            return IsAncestorOf(hunterNode, joinerNode) ? "ancestor" : "family";
        }

        private static bool IsAncestorOf(BloodfiendFamilyNode ancestorNode, BloodfiendFamilyNode descendantNode)
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode currentNode = registry.GetNode(descendantNode.embracerNodeId);
            while (currentNode != null)
            {
                if (currentNode == ancestorNode) return true;
                currentNode = registry.GetNode(currentNode.embracerNodeId);
            }
            return false;
        }

        private static void AddEnding(Quest quest, string signalName, QuestEndOutcome outcome, LetterDef letterDef, string keyPrefix, Pawn joiner, Pawn hunter)
        {
            string inSignal = QuestGenUtility.HardcodedSignalWithQuestID(signalName);
            quest.Letter(letterDef, inSignal, text: (keyPrefix + "Text").Translate(joiner.Named("JOINER"), hunter.Named("HUNTER")), label: (keyPrefix + "Label").Translate(joiner.Named("JOINER"), hunter.Named("HUNTER")));
            quest.End(outcome, inSignal: inSignal);
        }
    }
}