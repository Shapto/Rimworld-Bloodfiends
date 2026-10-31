using Bloodfiends.Bloodbags;
using Bloodfiends.Family;
using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Bloodfiends.Quests
{
    public class QuestNode_Root_BloodfiendTransport : QuestNode
    {
        private const int TicksToShuttleArrival = 180;
        private const int TicksToReinforcements = 10000;
        private const int TicksToBeginAssault = 5000;

        private static readonly SimpleCurve PointsToEscortCountCurve = new SimpleCurve
        {
            new CurvePoint(0f, 2f),
            new CurvePoint(2000f, 5f)
        };

        private static readonly SimpleCurve PointsToReinforcementsCountCurve = new SimpleCurve
        {
            new CurvePoint(2000f, 0f),
            new CurvePoint(2500f, 2f),
            new CurvePoint(5000f, 4f),
            new CurvePoint(8000f, 6f)
        };

        protected override bool TestRunInt(Slate slate)
        {
            Map map = QuestGen_Get.GetMap();
            return map != null && TryFindShuttleCrashPosition(map, ThingDefOf.ShuttleCrashed.size, out _);
        }

        protected override void RunInt()
        {
            if (!ModLister.CheckBiotech("Bloodfiend transport")) return;
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();
            float points = slate.Get("points", 0f);
            int endTicks = TicksToBeginAssault + 240;
            string questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("BloodfiendTransport");
            string attackedSignal = QuestGenUtility.HardcodedSignalWithQuestID("shuttlePawns.TookDamageFromPlayer");
            string defendTimeoutSignal = QuestGen.GenerateNewSignal("DefendTimeout");
            string beginAssaultSignal = QuestGen.GenerateNewSignal("BeginAssault");
            string assaultBeganSignal = QuestGen.GenerateNewSignal("AssaultBegan");
            string mapRemovedSignal = QuestGenUtility.HardcodedSignalWithQuestID("map.MapRemoved");
            slate.Set("map", map);

            List<FactionRelation> relations = Find.FactionManager.AllFactionsListForReading
                .Select(other => new FactionRelation(other, FactionRelationKind.Hostile))
                .ToList();
            Faction faction = FactionGenerator.NewGeneratedFactionWithRelations(FactionDefOf.Sanguophages, relations, hidden: true);
            faction.temporary = true;
            Find.FactionManager.Add(faction);
            quest.ReserveFaction(faction);

            var shuttlePawns = new List<Pawn>();
            Pawn master = quest.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Sanguophage, faction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true, allowPregnant: true, forcedXenotype: BloodfiendsDefOf.Bloodfiends_Bloodfiend));
            master.health.forceDowned = true;   // goes down instead of dying, so the player can take them
            shuttlePawns.Add(master);
            slate.Set("bloodfiend", master);
            int familyId = GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(master)?.familyId ?? -1;

            int escortCount = Mathf.RoundToInt(PointsToEscortCountCurve.Evaluate(points));
            for (int index = 0; index < escortCount; index++) shuttlePawns.Add(GenerateEscort(quest, faction, map, master, familyId));
            slate.Set("escortCount", escortCount);
            slate.Set("escortNoun", BloodbagUtility.BloodbagDef != null ? "Bloodbags" : "thralls");
            slate.Set("shuttlePawns", shuttlePawns);

            Thing shuttle = ThingMaker.MakeThing(ThingDefOf.ShuttleCrashed_Exitable);
            quest.SetFaction(Gen.YieldSingle(shuttle), faction);
            TryFindShuttleCrashPosition(map, shuttle.def.size, out IntVec3 crashPosition);
            TransportShip transportShip = quest.GenerateTransportShip(TransportShipDefOf.Ship_ShuttleCrashing, shuttlePawns, shuttle).transportShip;
            quest.AddShipJob_WaitTime(transportShip, 60, leaveImmediatelyWhenSatisfied: false).showGizmos = false;
            quest.AddShipJob(transportShip, ShipJobDefOf.Unload);
            QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);
            quest.End(QuestEndOutcome.Fail, inSignal: mapRemovedSignal);

            int reinforcementsCount = Mathf.RoundToInt(PointsToReinforcementsCountCurve.Evaluate(points));
            List<Pawn> reinforcements = null;
            if (reinforcementsCount > 0)
            {
                endTicks = TicksToReinforcements + 60;
                reinforcements = new List<Pawn>();
                for (int index = 0; index < reinforcementsCount; index++) reinforcements.Add(GenerateEscort(quest, faction, map, master, familyId));
                quest.BiocodeWeapons(reinforcements);
            }

            quest.Delay(TicksToShuttleArrival, () =>
            {
                quest.Letter(LetterDefOf.NegativeEvent, lookTargets: shuttlePawns, text: "[shuttleCrashedLetterText]", label: "[shuttleCrashedLetterLabel]");
                quest.AddShipJob_Arrive(transportShip, map.Parent, master, crashPosition, ShipJobStartMode.Force_DelayCurrent, faction);
                quest.DefendPoint(map.Parent, master, crashPosition, shuttlePawns, faction, null, null, 5f);
                quest.Delay(TicksToBeginAssault, () => quest.SignalPass(null, null, attackedSignal)).debugLabel = "Assault delay";
                quest.AnySignal(new[] { attackedSignal, defendTimeoutSignal }, null, Gen.YieldSingle(beginAssaultSignal));
                quest.SignalPassActivable(() =>
                {
                    quest.AnyPawnInCombatShape(shuttlePawns, () =>
                    {
                        QuestPart_AssaultColony assault = quest.AssaultColony(faction, map.Parent, shuttlePawns);
                        assault.canKidnap = false;
                        assault.canSteal = false;
                        assault.canTimeoutOrFlee = false;
                        quest.Letter(LetterDefOf.ThreatSmall, lookTargets: shuttlePawns, text: "[assaultBeginLetterText]", label: "[assaultBeginLetterLabel]");
                    }, null, null, assaultBeganSignal);
                }, null, beginAssaultSignal, null, null, assaultBeganSignal);

                if (reinforcementsCount > 0)
                {
                    quest.Delay(TicksToReinforcements, () =>
                    {
                        quest.Letter(LetterDefOf.ThreatBig, lookTargets: reinforcements, text: "[reinforcementsArrivedLetterText]", label: "[reinforcementsArrivedLetterLabel]");
                        DropCellFinder.TryFindRaidDropCenterClose(out IntVec3 dropSpot, map);
                        quest.DropPods(map.Parent, reinforcements, null, null, null, null, false, useTradeDropSpot: false, joinPlayer: false, makePrisoners: false, null, null, QuestPart.SignalListenMode.OngoingOnly, dropSpot);
                        QuestPart_AssaultColony reinforcementAssault = quest.AssaultColony(faction, map.Parent, reinforcements);
                        reinforcementAssault.canSteal = false;
                        reinforcementAssault.canTimeoutOrFlee = false;
                    }).debugLabel = "Reinforcements delay";
                }
                quest.Delay(endTicks, () => QuestGen_End.End(quest, QuestEndOutcome.Success)).debugLabel = "End delay";
            }, waitUntilPlayerHasHomeMap: true).debugLabel = "Arrival delay";
        }

        private static Pawn GenerateEscort(Quest quest, Faction faction, Map map, Pawn master, int familyId)
        {
            Pawn escort = quest.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.SanguophageThrall, faction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true, allowPregnant: true));
            if (BloodbagUtility.BloodbagDef != null && BloodbagUtility.CanBecomeBloodbag(escort)) BloodbagUtility.MakeBloodbag(escort, faction, master, familyId);
            return escort;
        }

        private static bool TryFindShuttleCrashPosition(Map map, IntVec2 size, out IntVec3 spot) => DropCellFinder.FindSafeLandingSpot(out spot, null, map, 35, 15, 25, size, ThingDefOf.ShuttleCrashed.interactionCellOffset);
    }
}