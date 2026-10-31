using Bloodfiends.Bloodbags;
using Bloodfiends.Family;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Bloodfiends.Quests
{
    public class QuestNode_Root_ElderHunt : QuestNode
    {
        private const float OwnElderChance = 0.75f;
        private const float ElderCombatPoints = 300f;
        private const int MaximumGuardCount = 8;
        private const float OwnElderHoardFactor = 0.5f;

        private static readonly IntRange TimeoutDaysRange = new IntRange(15, 30);
        private static readonly FloatRange HoardValueFactorRange = new FloatRange(1.2f, 1.8f);
        private static readonly IntRange HemogenPackCountRange = new IntRange(4, 10);

        protected override bool TestRunInt(Slate slate)
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            return registry != null && registry.AnyPlayerBloodfiend() && QuestGen_Get.GetMap() != null && TileFinder.TryFindNewSiteTile(out _);
        }

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            Map map = QuestGen_Get.GetMap();
            float points = slate.Get("points", 0f);
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile siteTile)) return;

            Faction lairFaction = MakeLairFaction(quest);

            List<BloodfiendFamilyNode> ownFamilyAncestors = registry.UnclaimedAncestorsOfPlayer();
            BloodfiendFamilyNode targetNode = ownFamilyAncestors.Count > 0 && Rand.Chance(OwnElderChance) ? ownFamilyAncestors.RandomElementByWeight(node => 1f / node.generationNumber) : null;

            Pawn elder = GenerateElder(lairFaction, siteTile, targetNode);
            BloodfiendFamilyNode elderNode = registry.GetNodeForPawn(elder);
            bool huntingOwnElder = targetNode != null && elderNode == targetNode;

            List<Pawn> guards = GenerateGuards(lairFaction, siteTile, elder, elderNode?.familyId ?? -1, points);
            List<Thing> hoard = GenerateHoard(points, huntingOwnElder);

            SitePartParams lairParams = new SitePartParams { threatPoints = points };
            Site site = QuestGen_Sites.GenerateSite(Gen.YieldSingle(new SitePartDefWithParams(BloodfiendsDefOf.Bloodfiends_ElderLair, lairParams)), siteTile, lairFaction);
            SitePart lairPart = site.parts[0];
            lairPart.things = new ThingOwner<Thing>(lairPart);
            lairPart.things.TryAdd(elder);
            foreach (Pawn guard in guards) lairPart.things.TryAdd(guard);
            foreach (Thing hoardThing in hoard) lairPart.things.TryAdd(hoardThing);

            slate.Set("map", map);
            slate.Set("site", site);
            slate.Set("elder", elder);
            slate.Set("ownFamily", huntingOwnElder);
            if (huntingOwnElder) slate.Set("descendant", registry.PlayerDescendantOf(elderNode));

            quest.ReserveFaction(lairFaction);
            quest.SpawnWorldObject(site);
            quest.AddPart(new QuestPart_ReleaseElderClaim { elder = elder });

            string mapGeneratedSignal = QuestGenUtility.HardcodedSignalWithQuestID("site.MapGenerated");
            string siteDestroyedSignal = QuestGenUtility.HardcodedSignalWithQuestID("site.Destroyed");
            string elderKilledSignal = QuestGenUtility.HardcodedSignalWithQuestID("elder.Killed");
            string elderCapturedSignal = QuestGenUtility.HardcodedSignalWithQuestID("elder.ChangedHostFaction");
            string elderEscapedSignal = QuestGenUtility.HardcodedSignalWithQuestID("elder.LeftMap");
            string timeoutSignal = QuestGen.GenerateNewSignal("ElderLairTimeout");

            QuestPart_WorldObjectTimeout timeout = quest.WorldObjectTimeout(site, TimeoutDaysRange.RandomInRange * GenDate.TicksPerDay, inSignalDisable: mapGeneratedSignal, outSignalsCompleted: new List<string> { timeoutSignal });
            timeout.destroyOnCleanup = true;

            AddEnding(quest, timeoutSignal, QuestEndOutcome.Fail, LetterDefOf.NeutralEvent, "Bloodfiends_ElderHuntExpired", elder);
            AddEnding(quest, siteDestroyedSignal, QuestEndOutcome.Fail, LetterDefOf.NeutralEvent, "Bloodfiends_ElderHuntAbandoned", elder);
            AddEnding(quest, elderEscapedSignal, QuestEndOutcome.Fail, LetterDefOf.NegativeEvent, "Bloodfiends_ElderHuntEscaped", elder);
            if (huntingOwnElder) quest.End(QuestEndOutcome.Success, inSignal: elderKilledSignal);
            else AddEnding(quest, elderKilledSignal, QuestEndOutcome.Success, LetterDefOf.PositiveEvent, "Bloodfiends_ElderHuntSlain", elder);
            AddEnding(quest, elderCapturedSignal, QuestEndOutcome.Success, LetterDefOf.PositiveEvent, "Bloodfiends_ElderHuntCaptured", elder);
        }

        private static Faction MakeLairFaction(Quest quest)
        {
            List<FactionRelation> relations = Find.FactionManager.AllFactionsListForReading
                .Select(faction => new FactionRelation(faction, FactionRelationKind.Hostile))
                .ToList();
            Faction lairFaction = FactionGenerator.NewGeneratedFactionWithRelations(FactionDefOf.Sanguophages, relations, hidden: true);
            lairFaction.temporary = true;
            Find.FactionManager.Add(lairFaction);
            return lairFaction;
        }

        private static Pawn GenerateElder(Faction lairFaction, PlanetTile siteTile, BloodfiendFamilyNode targetNode)
        {
            PawnGenerationRequest elderRequest = new PawnGenerationRequest(PawnKindDefOf.Sanguophage, lairFaction, PawnGenerationContext.NonPlayer, siteTile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true, forcedXenotype: BloodfiendsDefOf.Bloodfiends_Bloodfiend);

            GameComponent_BloodfiendFamilies.claimingNodeId = targetNode?.nodeId ?? -1;
            GameComponent_BloodfiendFamilies.foundingGeneration = targetNode == null ? GameComponent_BloodfiendFamilies.ElderGeneration : 0;
            try
            {
                Pawn elder = PawnGenerator.GeneratePawn(elderRequest);
                QuestGen.AddToGeneratedPawns(elder);
                return elder;
            }
            finally
            {
                GameComponent_BloodfiendFamilies.claimingNodeId = -1;
                GameComponent_BloodfiendFamilies.foundingGeneration = 0;
            }
        }

        private static List<Pawn> GenerateGuards(Faction lairFaction, PlanetTile siteTile, Pawn elder, int familyId, float points)
        {
            PawnKindDef guardKind = PawnKindDefOf.SanguophageThrall;
            int guardCount = Mathf.Clamp(Mathf.FloorToInt((points - ElderCombatPoints) / guardKind.combatPower), 1, MaximumGuardCount);

            List<Pawn> guards = new List<Pawn>();
            for (int guardIndex = 0; guardIndex < guardCount; guardIndex++)
            {
                Pawn guard = PawnGenerator.GeneratePawn(new PawnGenerationRequest(guardKind, lairFaction, PawnGenerationContext.NonPlayer, siteTile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
                if (BloodbagUtility.BloodbagDef != null) BloodbagUtility.MakeBloodbag(guard, lairFaction, elder, familyId);
                QuestGen.AddToGeneratedPawns(guard);
                guards.Add(guard);
            }
            return guards;
        }

        private static List<Thing> GenerateHoard(float points, bool huntingOwnElder)
        {
            float hoardValue = points * HoardValueFactorRange.RandomInRange * (huntingOwnElder ? OwnElderHoardFactor : 1f);
            ThingSetMakerParams hoardParams = new ThingSetMakerParams
            {
                totalMarketValueRange = new FloatRange(hoardValue * 0.9f, hoardValue * 1.1f),
                qualityGenerator = QualityGenerator.Reward
            };
            List<Thing> hoard = ThingSetMakerDefOf.Reward_ItemsStandard.root.Generate(hoardParams);

            Thing hemogenPacks = ThingMaker.MakeThing(ThingDefOf.HemogenPack);
            hemogenPacks.stackCount = HemogenPackCountRange.RandomInRange;
            hoard.Add(hemogenPacks);
            return hoard;
        }

        private static void AddEnding(Quest quest, string inSignal, QuestEndOutcome outcome, LetterDef letterDef, string keyPrefix, Pawn elder)
        {
            quest.Letter(letterDef, inSignal, text: (keyPrefix + "Text").Translate(elder.Named("ELDER")), label: (keyPrefix + "Label").Translate(elder.Named("ELDER")));
            quest.End(outcome, inSignal: inSignal);
        }
    }

    public class QuestPart_ReleaseElderClaim : QuestPart
    {
        public Pawn elder;

        public override void Cleanup()
        {
            base.Cleanup();
            if (elder == null || elder.Dead) return;
            if (!elder.Destroyed && !elder.Discarded && !(elder.ParentHolder is SitePart)) return;
            GameComponent_BloodfiendFamilies.Instance?.ReleaseClaim(elder);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref elder, "elder");
        }
    }
}