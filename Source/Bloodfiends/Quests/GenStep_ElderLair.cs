using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Bloodfiends.Quests
{
    public class GenStep_ElderLair : GenStep
    {
        private const float WanderRadius = 6f;
        private const float DefendRadius = 18f;
        private const int SpawnRadius = 5;

        public override int SeedPart => 518273946;

        public override void Generate(Map map, GenStepParams parms)
        {
            ThingOwner lairThings = parms.sitePart?.things;
            if (lairThings == null || lairThings.Count == 0) return;

            IntVec3 lairCenter = RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(cell => cell.Standable(map) && map.reachability.CanReachMapEdge(cell, TraverseParms.For(TraverseMode.PassDoors)), map, out IntVec3 foundCell) ? foundCell : map.Center;

            List<Pawn> lairPawns = new List<Pawn>();
            while (lairThings.Count > 0)
            {
                Thing lairThing = lairThings[0];
                lairThings.Remove(lairThing);

                if (lairThing is Pawn lairPawn)
                {
                    GenSpawn.Spawn(lairPawn, CellFinder.RandomClosewalkCellNear(lairCenter, map, SpawnRadius), map);
                    lairPawns.Add(lairPawn);
                }
                else GenPlace.TryPlaceThing(lairThing, lairCenter, map, ThingPlaceMode.Near);
            }

            if (lairPawns.Count > 0) LordMaker.MakeNewLord(lairPawns[0].Faction, new LordJob_DefendPoint(lairCenter, WanderRadius, DefendRadius, addFleeToil: false), map, lairPawns);
            MapGenerator.SetVar("RectOfInterest", CellRect.CenteredOn(lairCenter, SpawnRadius));
        }
    }
}