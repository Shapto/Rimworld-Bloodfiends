using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Aquaphobia
{
    public enum AquaphobiaExposure
    {
        None,
        Weak,
        Strong
    }

    /// <summary>
    /// Tunable in XML on the aquaphobia GeneDef.
    /// </summary>
    public class AquaphobiaExtension : DefModExtension
    {
        public float sightRadius = 19.9f;
        public int recheckIntervalTicks = 250;

        /// <summary>
        /// Terrain that causes weak exposure, like marsh and mud. Open water is always strong.
        /// </summary>
        public List<TerrainDef> weakTerrains = new List<TerrainDef>();
    }

    /// <summary>
    /// Fear of water. Strong exposure: open water in sight, or outdoors in rain. Weak exposure: snow, snowfall, marsh or mud.
    /// Vanilla rolls the mental break through this GeneDef's mentalBreakMtbDays; the break worker only allows it on strong exposure.
    /// </summary>
    public class Gene_Aquaphobia : Gene
    {
        private static readonly AquaphobiaExtension DefaultSettings = new AquaphobiaExtension();

        private AquaphobiaExposure cachedExposure;
        private int lastCheckTick = -99999;

        private AquaphobiaExtension Settings => def.GetModExtension<AquaphobiaExtension>() ?? DefaultSettings;

        public AquaphobiaExposure Exposure
        {
            get
            {
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastCheckTick >= Settings.recheckIntervalTicks)
                {
                    cachedExposure = CalculateExposure();
                    lastCheckTick = currentTick;
                }
                return cachedExposure;
            }
        }

        private AquaphobiaExposure CalculateExposure()
        {
            if (!pawn.Spawned) return AquaphobiaExposure.None;

            Map map = pawn.Map;
            bool isOutdoors = !pawn.Position.Roofed(map);
            if (isOutdoors && map.weatherManager.RainRate > 0f) return AquaphobiaExposure.Strong;

            AquaphobiaExposure exposure = isOutdoors && map.weatherManager.SnowRate > 0f ? AquaphobiaExposure.Weak : AquaphobiaExposure.None;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, Settings.sightRadius, true))
            {
                if (!cell.InBounds(map) || cell.Fogged(map)) continue;

                AquaphobiaExposure cellExposure = GetCellExposure(cell, map);
                if (cellExposure <= exposure || !GenSight.LineOfSight(pawn.Position, cell, map, skipFirstCell: true)) continue;

                exposure = cellExposure;
                if (exposure == AquaphobiaExposure.Strong) break;
            }
            return exposure;
        }

        private AquaphobiaExposure GetCellExposure(IntVec3 cell, Map map)
        {
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain.IsWater) return AquaphobiaExposure.Strong;
            if (Settings.weakTerrains.Contains(terrain) || map.snowGrid.GetDepth(cell) > 0f) return AquaphobiaExposure.Weak;
            return AquaphobiaExposure.None;
        }
    }
}
