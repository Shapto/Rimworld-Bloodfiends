using Bloodfiends.Family;
using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Bloodfiends.Quests
{
    public class QuestNode_Root_FamilyGathering : QuestNode
    {
        private const float ExtraRaidPointsPerBloodfiend = 200f;
        private const int JoinerWaitDurationTicks = 7500;
        private const int EmbraceWaitDurationTicks = 15000;

        private static readonly List<CountChance> BloodfiendCountChances = new List<CountChance>
        {
            new CountChance { count = 2, chance = 0.5f },
            new CountChance { count = 3, chance = 0.3f },
            new CountChance { count = 4, chance = 0.15f },
            new CountChance { count = 5, chance = 0.05f }
        };

        private static readonly IntRange MeetingDurationRange = new IntRange(10000, 20000);
        private static readonly IntRange EventDelayRange = new IntRange(5000, 7500);
        private static readonly IntRange ArrivalDelayRange = new IntRange(2500, 7500);

        private static readonly SimpleCurve RewardValueCurve = new SimpleCurve
        {
            new CurvePoint(500f, 400f),
            new CurvePoint(3000f, 2000f),
            new CurvePoint(30000f, 10000f)
        };

        protected override bool TestRunInt(Slate slate)
        {
            Map map = QuestGen_Get.GetMap();
            return map != null && FactionDefOf.Sanguophages.allowedArrivalTemperatureRange.Includes(map.mapTemperature.OutdoorTemp) && GatheringSpotFinder.TryFind(map, out _);
        }

        protected override void RunInt()
        {
            if (!ModLister.CheckBiotech("Bloodfiend family gathering")) return;
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();
            float points = slate.Get("points", 0f);
            string questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("FamilyGathering");
            slate.Set("map", map);
            if (!GatheringSpotFinder.TryFind(map, out IntVec3 gatherSpot)) return;
            slate.Set("gatherSpot", gatherSpot);

            string meetingStartedSignal = QuestGen.GenerateNewSignal("MeetingStarted");
            string meetingCompletedSignal = QuestGen.GenerateNewSignal("MeetingCompleted");
            string raidArrivedSignal = QuestGen.GenerateNewSignal("RaidArrived");
            string beginDefendSignal = QuestGen.GenerateNewSignal("BeginDefend");
            string becameHostileSignal = QuestGen.GenerateNewSignal("BloodfiendsBecameHostile");
            string questFailedSignal = QuestGen.GenerateNewSignal("QuestFailed");
            string questSucceededSignal = QuestGen.GenerateNewSignal("QuestSucceeded");
            string allGoneSignal = QuestGen.GenerateNewSignal("AllBloodfiendsGone");
            string embracedSignal = QuestGen.GenerateNewSignal("ColonistEmbraced");
            string beingAttackedSignal = QuestGenUtility.QuestTagSignal(questTag, "BeingAttacked");
            string arrestedSignal = QuestGenUtility.HardcodedSignalWithQuestID("bloodfiends.Arrested");
            string swappedMapSignal = QuestGenUtility.HardcodedSignalWithQuestID("bloodfiends.SwappedMap");
            string surgeryViolationSignal = QuestGenUtility.HardcodedSignalWithQuestID("bloodfiends.SurgeryViolation");
            string mapRemovedSignal = QuestGenUtility.HardcodedSignalWithQuestID("map.MapRemoved");
            string enemiesDespawnedSignal = QuestGenUtility.HardcodedSignalWithQuestID("enemies.Despawned");
            string enemyArrestedSignal = QuestGenUtility.HardcodedSignalWithQuestID("enemies.Arrested");
            int meetingDurationTicks = MeetingDurationRange.RandomInRange;

            List<FactionRelation> relations = Find.FactionManager.AllFactionsListForReading
                .Where(other => !other.def.PermanentlyHostileTo(FactionDefOf.Sanguophages))
                .Select(other => new FactionRelation(other, FactionRelationKind.Neutral))
                .ToList();
            Faction faction = FactionGenerator.NewGeneratedFactionWithRelations(FactionDefOf.Sanguophages, relations, hidden: true);
            faction.temporary = true;
            Find.FactionManager.Add(faction);
            slate.Set("faction", faction);
            quest.ReserveFaction(faction);

            List<Pawn> bloodfiends = GenerateFamily(quest, faction, map, CountChanceUtility.RandomCount(BloodfiendCountChances));
            slate.Set("bloodfiends", bloodfiends);
            slate.Set("bloodfiendCount", bloodfiends.Count);
            slate.Set("bloodfiendCountMinusOne", bloodfiends.Count - 1);
            Pawn asker = bloodfiends.First();
            slate.Set("asker", asker);

            Faction enemyFaction = null;
            List<Pawn> enemies = null;
            if (Find.Storyteller.difficulty.allowViolentQuests && Find.FactionManager.AllFactionsVisible.Where(candidate => candidate.def.permanentEnemy).TryRandomElement(out enemyFaction))
            {
                slate.Set("enemyFaction", enemyFaction);
                var groupParms = new PawnGroupMakerParms
                {
                    faction = enemyFaction,
                    groupKind = PawnGroupKindDefOf.Combat,
                    points = IncidentWorker_Raid.AdjustedRaidPoints(points + ExtraRaidPointsPerBloodfiend * bloodfiends.Count, PawnsArrivalModeDefOf.EdgeWalkIn, RaidStrategyDefOf.ImmediateAttack, enemyFaction, PawnGroupKindDefOf.Combat, map),
                    tile = map.Tile
                };
                enemies = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
                if (enemies.Any())
                {
                    foreach (Pawn enemy in enemies)
                    {
                        Find.WorldPawns.PassToWorld(enemy);
                        QuestGen.AddToGeneratedPawns(enemy);
                    }
                    slate.Set("enemies", enemies);
                    var enemiesArrive = new QuestPart_PawnsArrive
                    {
                        arrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                        mapParent = map.Parent,
                        inSignal = raidArrivedSignal,
                        sendStandardLetter = false,
                        addPawnsToLookTargets = false
                    };
                    enemiesArrive.pawns.AddRange(enemies);
                    quest.AddPart(enemiesArrive);
                    quest.AssaultThings(map.Parent, enemies, enemyFaction, bloodfiends, raidArrivedSignal, null, excludeFromLookTargets: true);
                    quest.Letter(LetterDefOf.ThreatBig, raidArrivedSignal, relatedFaction: enemyFaction, lookTargets: enemies, text: "[raidArrivedLetterText]", label: "[raidArrivedLetterLabel]");
                    quest.AllPawnsDespawned(enemies, () => quest.SignalPass(null, null, meetingCompletedSignal), null, enemiesDespawnedSignal, null, null, enemyArrestedSignal);
                }
            }

            quest.Message("Bloodfiends_GatheringArrivingSoon".Translate(), MessageTypeDefOf.NeutralEvent).historical = false;
            quest.Delay(ArrivalDelayRange.RandomInRange, () =>
            {
                quest.PawnsArrive(bloodfiends, null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkInGroups, customLetterLabel: "[bloodfiendsArriveLetterLabel]", customLetterText: "[bloodfiendsArriveLetterText]");
                var gathering = new QuestPart_FamilyGathering
                {
                    inSignal = QuestGen.slate.Get<string>("inSignal"),
                    questTag = questTag,
                    faction = faction,
                    targetCell = gatherSpot,
                    meetingDurationTicks = meetingDurationTicks,
                    mapParent = map.Parent,
                    inSignalDefend = beginDefendSignal,
                    inSignalQuestSuccess = meetingCompletedSignal,
                    inSignalQuestFail = questFailedSignal,
                    outSignalMeetingStarted = meetingStartedSignal,
                    outSignalMeetingCompleted = meetingCompletedSignal,
                    outSignalAllSanguophagesGone = allGoneSignal
                };
                gathering.pawns.AddRange(bloodfiends);
                if (enemies != null) gathering.enemyTargets.AddRange(enemies);
                quest.AddPart(gathering);
            }).debugLabel = "Arrival delay";

            quest.Delay(EventDelayRange.RandomInRange, () =>
            {
                var events = new List<Action>
                {
                    () => { },
                    () => OfferJoiner(quest, map, faction, bloodfiends, gatherSpot, meetingCompletedSignal)
                };
                if (enemyFaction != null)
                {
                    events.Add(() =>
                    {
                        quest.SignalPass(null, null, raidArrivedSignal);
                        quest.Delay(1250, () => quest.SignalPass(null, null, beginDefendSignal)).debugLabel = "Defend delay";
                    });
                }
                quest.SignalRandom(events);
            }, meetingStartedSignal).debugLabel = "Event delay";

            var rewardParms = new RewardsGeneratorParams
            {
                rewardValue = RewardValueCurve.Evaluate(points),
                thingRewardRequired = true,
                allowXenogermReimplantation = false
            };
            QuestPart_Choice rewardChoice = quest.GiveRewards(rewardParms, questSucceededSignal, asker: asker, overridePopulationIntent: 0f, variants: 2);
            AddEmbraceChoice(quest, rewardChoice, map, faction, bloodfiends, gatherSpot, questSucceededSignal, embracedSignal);

            quest.AnySignal(new[] { embracedSignal, meetingCompletedSignal }, null, new[] { questSucceededSignal });
            quest.Letter(LetterDefOf.PositiveEvent, questSucceededSignal, lookTargets: bloodfiends, filterDeadPawnsFromLookTargets: true, text: "[successLetterText]", label: "[successLetterLabel]");
            quest.End(QuestEndOutcome.Success, inSignal: questSucceededSignal);
            quest.AnySignal(new[] { arrestedSignal, surgeryViolationSignal, beingAttackedSignal }, null, new[] { becameHostileSignal, questFailedSignal });
            quest.FactionRelationToPlayerChange(faction, FactionRelationKind.Hostile, false, becameHostileSignal);
            quest.End(QuestEndOutcome.Fail, inSignal: becameHostileSignal);
            quest.End(QuestEndOutcome.Fail, inSignal: mapRemovedSignal);
            quest.End(QuestEndOutcome.Fail, inSignal: allGoneSignal);
            quest.End(QuestEndOutcome.Fail, inSignal: swappedMapSignal);
        }

        private static List<Pawn> GenerateFamily(Quest quest, Faction faction, Map map, int count)
        {
            var request = new PawnGenerationRequest(PawnKindDefOf.Sanguophage, faction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true, forcedXenotype: BloodfiendsDefOf.Bloodfiends_Bloodfiend);
            var bloodfiends = new List<Pawn> { quest.GeneratePawn(request) };
            GameComponent_BloodfiendFamilies families = GameComponent_BloodfiendFamilies.Instance;
            GameComponent_BloodfiendFamilies.placingIntoFamily = families?.GetNodeForPawn(bloodfiends[0])?.familyId ?? -1;
            try
            {
                for (int index = 1; index < count; index++) bloodfiends.Add(quest.GeneratePawn(request));
            }
            finally
            {
                GameComponent_BloodfiendFamilies.placingIntoFamily = -1;
            }
            return bloodfiends
                .OrderBy(pawn => families?.GetNodeForPawn(pawn)?.generationNumber ?? int.MaxValue)
                .ToList();
        }

        private static void OfferJoiner(Quest quest, Map map, Faction faction, List<Pawn> bloodfiends, IntVec3 gatherSpot, string meetingCompletedSignal)
        {
            Pawn joiner = bloodfiends.FirstOrDefault(pawn => !pawn.Dead && !pawn.Downed);
            if (joiner == null) return;
            quest.WaitForDuration(map.Parent, bloodfiends, faction, gatherSpot, JoinerWaitDurationTicks);
            quest.PawnJoinOffer(joiner, "LetterJoinOfferLabel".Translate(joiner.Named("PAWN")), "LetterJoinOfferTitle".Translate(joiner.Named("PAWN")), "LetterJoinOfferText".Translate(joiner.Named("PAWN"), map.Parent.Named("MAP")).Resolve(), () =>
            {
                quest.JoinPlayer(map.Parent, Gen.YieldSingle(joiner), joinPlayer: true);
                quest.Letter(LetterDefOf.PositiveEvent, label: "LetterLabelMessageRecruitSuccess".Translate() + ": " + joiner.LabelShortCap, text: "MessageRecruitJoinOfferAccepted".Translate(joiner.Named("RECRUITEE")).Resolve());
                quest.SignalPass(null, null, meetingCompletedSignal);
            }, () => quest.SignalPass(null, null, meetingCompletedSignal), sendLetterOnEnable: true);
            quest.Delay(JoinerWaitDurationTicks, () => quest.SignalPass(null, null, meetingCompletedSignal), null, meetingCompletedSignal);
        }

        private static void AddEmbraceChoice(Quest quest, QuestPart_Choice rewardChoice, Map map, Faction faction, List<Pawn> bloodfiends, IntVec3 gatherSpot, string successSignal, string embracedSignal)
        {
            if (rewardChoice == null) return;
            var embraceChoice = new QuestPart_Choice.Choice();
            embraceChoice.rewards.Add(new Reward_FamilyEmbrace());
            var embracePart = new QuestPart_FamilyEmbrace
            {
                inSignal = successSignal,
                mapParent = map.Parent,
                faction = faction,
                gatherSpot = gatherSpot,
                waitDurationTicks = EmbraceWaitDurationTicks,
                inSignalEmbraced = embracedSignal,
                inSignalRemovePawn = QuestGenUtility.HardcodedSignalWithQuestID("bloodfiends.ChangedFactionToPlayer")
            };
            embracePart.pawns.AddRange(bloodfiends);
            quest.AddPart(embracePart);
            embraceChoice.questParts.Add(embracePart);
            rewardChoice.choices.Insert(0, embraceChoice);
        }
    }
    public static class GatheringSpotFinder
    {
        public static bool TryFind(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null) return false;
            List<Room> rooms = map.listerBuildings.allBuildingsColonist
                .Select(building => building.Position.GetRoom(map))
                .Where(room => room != null)
                .Distinct()
                .OrderByDescending(RoomScore)
                .ToList();
            foreach (Room room in rooms)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    IntVec3 candidate = CellFinder.RandomClosewalkCellNear(room.Cells.RandomElement(), map, 5, nearCell => IsOpenSpot(nearCell, map));
                    if (!map.reachability.CanReachColony(candidate)) continue;
                    cell = candidate;
                    return true;
                }
            }
            return RCellFinder.TryFindRandomSpotJustOutsideColony(CellFinder.RandomEdgeCell(map), map, out cell);
        }

        private static bool IsOpenSpot(IntVec3 cell, Map map)
        {
            if (cell.GetEdifice(map) != null) return false;
            foreach (IntVec3 offset in GenAdj.AdjacentCellsAround)
            {
                IntVec3 neighbour = cell + offset;
                if (!neighbour.InBounds(map) || neighbour.Fogged(map) || !neighbour.Standable(map)) return false;
            }
            return true;
        }

        private static float RoomScore(Room room)
        {
            if (room.CellCount < 10 || room.PsychologicallyOutdoors) return -100f;
            if (room.Role == RoomRoleDefOf.Barracks || room.Role == RoomRoleDefOf.Bedroom) return -100f;
            if (room.Role == RoomRoleDefOf.PrisonCell || room.Role == RoomRoleDefOf.PrisonBarracks) return -1000f;
            return room.GetStat(RoomStatDefOf.Impressiveness) + room.GetStat(RoomStatDefOf.Beauty);
        }
    }
}