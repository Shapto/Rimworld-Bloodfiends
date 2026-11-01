using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace Bloodfiends.Sanguophages
{
    /// <summary>
    /// With the replacement setting on, any pawn that would generate as a sanguophage generates as a Bloodfiend instead.
    /// The mod's own quests force the Bloodfiend xenotype directly, so they are unaffected.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GetXenotypeForGeneratedPawn))]
    public static class Patch_PawnGenerator_GetXenotypeForGeneratedPawn
    {
        public static void Postfix(ref XenotypeDef __result)
        {
            if (BloodfiendsMod.Settings.replaceSanguophages && __result == XenotypeDefOf.Sanguophage) __result = BloodfiendsDefOf.Bloodfiends_Bloodfiend;
        }
    }

    /// <summary>
    /// With the replacement setting on, vanilla's sanguophage quests can't run
    /// </summary>
    [HarmonyPatch(typeof(QuestScriptDef), nameof(QuestScriptDef.CanRun), typeof(Slate), typeof(IIncidentTarget))]
    public static class Patch_QuestScriptDef_CanRun
    {
        private static readonly HashSet<string> ReplacedQuestNames = new HashSet<string> { "SanguophageMeetingHost", "SanguophageShip" };

        public static void Postfix(QuestScriptDef __instance, ref bool __result)
        {
            if (__result && BloodfiendsMod.Settings.replaceSanguophages && ReplacedQuestNames.Contains(__instance.defName)) __result = false;
        }
    }
}