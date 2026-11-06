using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Bloodfiends.Spawning
{
    [StaticConstructorOnStartup]
    public static class BloodfiendFactionSpawning
    {
        private static readonly AccessTools.FieldRef<XenotypeSet, List<XenotypeChance>> ChancesOf = AccessTools.FieldRefAccess<XenotypeSet, List<XenotypeChance>>("xenotypeChances");
        private static readonly List<XenotypeChance> addedChances = new List<XenotypeChance>();

        static BloodfiendFactionSpawning()
        {
            IEnumerable<FactionDef> factionDefs = DefDatabase<FactionDef>.AllDefs
                .Where(factionDef => factionDef.humanlikeFaction && !factionDef.isPlayer && !factionDef.hidden);
            foreach (FactionDef factionDef in factionDefs)
            {
                if (factionDef.xenotypeSet == null) factionDef.xenotypeSet = new XenotypeSet();
                if (ChancesOf(factionDef.xenotypeSet) == null) ChancesOf(factionDef.xenotypeSet) = new List<XenotypeChance>();
                List<XenotypeChance> chances = ChancesOf(factionDef.xenotypeSet);
                if (chances.Any(entry => entry.xenotype == BloodfiendsDefOf.Bloodfiends_Bloodfiend)) continue;

                XenotypeChance bloodfiendChance = new XenotypeChance { xenotype = BloodfiendsDefOf.Bloodfiends_Bloodfiend, chance = 0f };
                chances.Add(bloodfiendChance);
                addedChances.Add(bloodfiendChance);
            }
            ApplyChance();
        }

        /// <summary>
        /// Pushes the current setting into every faction's Bloodfiend entry.
        /// </summary>
        public static void ApplyChance()
        {
            foreach (XenotypeChance bloodfiendChance in addedChances) bloodfiendChance.chance = BloodfiendsMod.Settings.factionSpawnChance;
        }
    }
}