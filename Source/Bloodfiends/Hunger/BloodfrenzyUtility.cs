using RimWorld;
using Verse;

namespace Bloodfiends.Hunger
{
    public static class BloodfrenzyUtility
    {
        private const float WitheringPerHemogen = 0.1f;

        public static bool IsFrenzied(Pawn pawn) => pawn?.MentalState is MentalState_Bloodfrenzy;

        public static float HemogenOf(Pawn pawn) => pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>()?.Value ?? 0f;

        public static bool CanAfford(Pawn pawn, float hemogenCost) => IsFrenzied(pawn) || HemogenOf(pawn) >= hemogenCost;

        public static void Pay(Pawn pawn, float hemogenCost)
        {
            if (!IsFrenzied(pawn))
            {
                GeneUtility.OffsetHemogen(pawn, -hemogenCost);
                return;
            }

            Gene_Withering witheringGene = pawn.genes?.GetFirstGeneOfType<Gene_Withering>();
            Hediff withering = witheringGene == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(witheringGene.Settings.witheringHediff);
            if (withering != null) withering.Severity += hemogenCost * WitheringPerHemogen;
        }
    }
}