using HarmonyLib;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    /// <summary>
    /// Stops the world pawn cleanup from discarding a Bloodfiend while one of the player's Bloodfiends descends from them.
    /// Everything else about relevance (maps, caravans, quests, faction leaders) is already handled by vanilla.
    /// </summary>
    [HarmonyPatch(typeof(WorldPawnGC), "GetCriticalPawnReason")]
    public static class Patch_WorldPawnGC_KeepBloodfiendAncestors
    {
        public static void Postfix(Pawn pawn, ref string __result)
        {
            if (__result != null) return;
            if (GameComponent_BloodfiendFamilies.Instance?.IsAncestorOfPlayerBloodfiend(pawn) == true) __result = "Bloodfiend ancestor of a colony Bloodfiend";
        }
    }
}
