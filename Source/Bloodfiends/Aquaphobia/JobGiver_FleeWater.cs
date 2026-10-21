using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Bloodfiends.Aquaphobia
{
    public class JobGiver_FleeWater : ThinkNode_JobGiver
    {
        private const float SearchRadius = 30f;
        private const float WaterClearanceRadius = 6f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.Spawned) return null;

            Map map = pawn.Map;
            bool needsRoof = map.weatherManager.RainRate > 0f;

            bool foundSafeCell = CellFinder.TryFindRandomReachableNearbyCell(pawn.Position, map, SearchRadius, TraverseParms.For(pawn), cell => IsSafeFromWater(cell, map, needsRoof), null, out IntVec3 safeCell);
            if (!foundSafeCell) return null;

            Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, safeCell);
            fleeJob.locomotionUrgency = LocomotionUrgency.Jog;
            return fleeJob;
        }

        private static bool IsSafeFromWater(IntVec3 cell, Map map, bool needsRoof)
        {
            if (!cell.Standable(map) || cell.GetTerrain(map).IsWater) return false;
            if (needsRoof && !cell.Roofed(map)) return false;

            foreach (IntVec3 nearbyCell in GenRadial.RadialCellsAround(cell, WaterClearanceRadius, true))
            {
                if (nearbyCell.InBounds(map) && nearbyCell.GetTerrain(map).IsWater) return false;
            }
            return true;
        }
    }
}
