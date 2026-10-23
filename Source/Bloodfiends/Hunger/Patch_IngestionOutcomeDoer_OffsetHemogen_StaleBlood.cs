using Bloodfiends.Bloodbags;
using Bloodfiends.Family;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Hunger
{
    /// <summary>
    /// How much stale blood still feeds a Bloodfiend (on the Bloodfiend gene).
    /// </summary>
    public class StaleBloodExtension : DefModExtension
    {
        public float hemogenFactor = 0.25f;
    }

    /// <summary>
    /// Bloodfiends feed on the emotions in living blood, not the blood itself, so stored blood (hemogen packs) barely
    /// feeds them.
    /// </summary>
    [HarmonyPatch(typeof(IngestionOutcomeDoer_OffsetHemogen), "DoIngestionOutcomeSpecial")]
    public static class Patch_IngestionOutcomeDoer_OffsetHemogen_StaleBlood
    {
        private static float? cachedFactor;

        public static float Factor
        {
            get
            {
                if (cachedFactor == null) cachedFactor = DefDatabase<GeneDef>.GetNamed("Bloodfiends_Bloodfiend").GetModExtension<StaleBloodExtension>()?.hemogenFactor ?? 0.25f;
                return cachedFactor.Value;
            }
        }

        public static bool Prefix(IngestionOutcomeDoer_OffsetHemogen __instance, Pawn pawn, int ingestedCount)
        {
            bool feedsOnEmotion = pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active == true || BloodbagUtility.IsBloodbag(pawn);
            if (!feedsOnEmotion) return true;
            if (Factor > 0f) GeneUtility.OffsetHemogen(pawn, __instance.offset * ingestedCount * Factor);
            return false;
        }
    }
}
