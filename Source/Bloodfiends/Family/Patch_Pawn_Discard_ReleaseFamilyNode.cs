using HarmonyLib;
using Verse;

namespace Bloodfiends.Family
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Discard))]
    public static class Patch_Pawn_Discard_ReleaseFamilyNode
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance.Dead) return;
            GameComponent_BloodfiendFamilies.Instance?.ReleaseClaim(__instance);
        }
    }
}