using Verse;

namespace Bloodfiends
{
    public class BloodfiendsSettings : ModSettings
    {
        public const int DefaultMaximumKindredsPerBloodfiend = 2;

        public bool replaceSanguophages = false;
        public bool regenerationSurvivesBrainDestruction = false;
        public int maximumKindredsPerBloodfiend = DefaultMaximumKindredsPerBloodfiend;
        public bool firstKindredDropsPrimogenitorBlood = true;
        public bool fireHindersRegeneration = true;
        public bool kindredSlotsFreeOnDeath = true;

        public void ResetToDefaults()
        {
            replaceSanguophages = false;
            regenerationSurvivesBrainDestruction = false;
            maximumKindredsPerBloodfiend = DefaultMaximumKindredsPerBloodfiend;
            firstKindredDropsPrimogenitorBlood = true;
            fireHindersRegeneration = true;
            kindredSlotsFreeOnDeath = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref replaceSanguophages, "replaceSanguophages", false);
            Scribe_Values.Look(ref regenerationSurvivesBrainDestruction, "regenerationSurvivesBrainDestruction", false);
            Scribe_Values.Look(ref maximumKindredsPerBloodfiend, "maximumKindredsPerBloodfiend", DefaultMaximumKindredsPerBloodfiend);
            Scribe_Values.Look(ref firstKindredDropsPrimogenitorBlood, "firstKindredDropsPrimogenitorBlood", true);
            Scribe_Values.Look(ref fireHindersRegeneration, "fireHindersRegeneration", true);
        }
    }
}