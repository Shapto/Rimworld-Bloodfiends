using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Bloodfiends.Quests
{
    public class GenStep_ElderLair : GenStep
    {
        private const int LairHalfSize = 5;
        private const int BloodFilthCount = 14;
        private const float DefendRadius = 16f;
        private const float WanderRadius = 4f;

        public override int SeedPart => 518273946;

        public override void Generate(Map map, GenStepParams parms)
        {
            ThingOwner lairThings = parms.sitePart?.things;
            if (lairThings == null || lairThings.Count == 0) return;

            IntVec3 lairCenter = RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(cell => CanBuildLairAt(cell, map), map, out IntVec3 foundCell) ? foundCell : map.Center;
            CellRect lairRect = CellRect.CenteredOn(lairCenter, LairHalfSize).ClipInsideMap(map);
            Faction lairFaction = lairThings.OfType<Pawn>().FirstOrDefault()?.Faction;
            BuildLair(map, lairRect, lairFaction);

            List<IntVec3> interiorCells = lairRect.ContractedBy(1).Cells
                .Where(cell => cell.Standable(map))
                .ToList();
            List<Pawn> lairPawns = new List<Pawn>();
            bool elderPlaced = false;
            while (lairThings.Count > 0)
            {
                Thing lairThing = lairThings[0];
                lairThings.Remove(lairThing);

                if (lairThing is Pawn lairPawn)
                {
                    IntVec3 spawnCell = !elderPlaced ? lairCenter : interiorCells.Count > 0 ? interiorCells.RandomElement() : CellFinder.RandomClosewalkCellNear(lairCenter, map, LairHalfSize);
                    GenSpawn.Spawn(lairPawn, spawnCell, map);
                    lairPawns.Add(lairPawn);
                    elderPlaced = true;
                }
                else GenPlace.TryPlaceThing(lairThing, lairCenter + IntVec3.East * 2, map, ThingPlaceMode.Near);
            }

            if (lairPawns.Count > 0) LordMaker.MakeNewLord(lairFaction, new LordJob_ElderLair(lairCenter, DefendRadius, WanderRadius), map, lairPawns);
            MapGenerator.SetVar("RectOfInterest", lairRect);
        }

        private static bool CanBuildLairAt(IntVec3 center, Map map)
        {
            CellRect footprint = CellRect.CenteredOn(center, LairHalfSize + 2);
            if (!footprint.InBounds(map)) return false;
            return footprint.Cells.All(cell => cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy));
        }

        private static void BuildLair(Map map, CellRect lairRect, Faction lairFaction)
        {
            ThingDef wallStuff = BaseGenUtility.RandomCheapWallStuff(lairFaction, notVeryFlammable: true);
            HashSet<IntVec3> doorCells = new HashSet<IntVec3>
            {
                new IntVec3(lairRect.CenterCell.x, 0, lairRect.minZ),
                new IntVec3(lairRect.CenterCell.x, 0, lairRect.maxZ)
            };

            foreach (IntVec3 cell in lairRect.Cells)
            {
                foreach (Thing existingThing in cell.GetThingList(map).ToList())
                    if (existingThing.def.destroyable && !(existingThing is Pawn)) existingThing.Destroy();
                if (cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy)) map.terrainGrid.SetTerrain(cell, TerrainDefOf.AncientTile);
            }

            foreach (IntVec3 edgeCell in lairRect.EdgeCells)
            {
                Thing wallOrDoor = ThingMaker.MakeThing(doorCells.Contains(edgeCell) ? ThingDefOf.Door : ThingDefOf.Wall, wallStuff);
                wallOrDoor.SetFaction(lairFaction);
                GenSpawn.Spawn(wallOrDoor, edgeCell, map);
            }

            foreach (IntVec3 cell in lairRect.Cells) map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);

            CellRect interiorRect = lairRect.ContractedBy(1);
            if (ThingDefOf.SanguphageMeetingTorch != null)
            {
                foreach (IntVec3 cornerCell in interiorRect.Corners)
                {
                    Thing torch = ThingMaker.MakeThing(ThingDefOf.SanguphageMeetingTorch);
                    torch.SetFaction(lairFaction);
                    GenSpawn.Spawn(torch, cornerCell, map);
                }
            }

            for (int filthIndex = 0; filthIndex < BloodFilthCount; filthIndex++) FilthMaker.TryMakeFilth(interiorRect.RandomCell, map, ThingDefOf.Filth_Blood);
        }
    }

    public class LordJob_ElderLair : LordJob
    {
        private IntVec3 lairCenter;
        private float defendRadius;
        private float wanderRadius;

        public LordJob_ElderLair()
        {
        }

        public LordJob_ElderLair(IntVec3 lairCenter, float defendRadius, float wanderRadius)
        {
            this.lairCenter = lairCenter;
            this.defendRadius = defendRadius;
            this.wanderRadius = wanderRadius;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil_DefendPoint holdLair = new LordToil_DefendPoint(lairCenter, defendRadius, wanderRadius);
            graph.StartingToil = holdLair;
            LordToil_AssaultColony attack = new LordToil_AssaultColony(attackDownedIfStarving: true);
            graph.AddToil(attack);

            Transition provoked = new Transition(holdLair, attack);
            provoked.AddTrigger(new Trigger_PawnHarmed());
            provoked.AddTrigger(new Trigger_FractionPawnsLost(0.01f));
            provoked.AddPreAction(new TransitionAction_Message("Bloodfiends_ElderLairProvoked".Translate(), MessageTypeDefOf.ThreatBig));
            provoked.AddPostAction(new TransitionAction_WakeAll());
            graph.AddTransition(provoked);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lairCenter, "lairCenter");
            Scribe_Values.Look(ref defendRadius, "defendRadius");
            Scribe_Values.Look(ref wanderRadius, "wanderRadius");
        }
    }
}