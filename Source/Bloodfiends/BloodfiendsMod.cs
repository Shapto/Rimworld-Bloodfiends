using Bloodfiends.Bloodbags;
using HarmonyLib;
using SingularityFramework.WeaponAnchor;
using UnityEngine;
using Verse;

namespace Bloodfiends
{
    public class BloodfiendsMod : Mod
    {
        private const int MinimumKindredsPerBloodfiend = 1;
        private const int MaximumKindredsSliderValue = 5;

        public static BloodfiendsSettings Settings { get; private set; }

        private static Vector2 settingsScrollPosition;
        private static float settingsContentHeight = 1000f;

        public BloodfiendsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BloodfiendsSettings>();
            new Harmony("shapto.bloodfiends").PatchAll();
            WeaponOverlays.Register(Bloodfiends.Hardblood.HardbloodGraphics.BloodbagCoating);
        }

        public override string SettingsCategory() => "Bloodfiends";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, settingsContentHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            // Much taller than the settings will ever be, so the listing never wraps into a second column.
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(0f, 0f, viewRect.width, 99999f));

            listing.Label("Bloodfiends_SettingFactionSpawnChance".Translate(Settings.factionSpawnChance.ToStringPercent("0.0")));
            Settings.factionSpawnChance = Mathf.Round(listing.Slider(Settings.factionSpawnChance, 0f, 1f) * 1000f) / 1000f;   // 0.1% steps
            Spawning.BloodfiendFactionSpawning.ApplyChance();
            listing.CheckboxLabeled("Bloodfiends_SettingReplaceSanguophages".Translate(), ref Settings.replaceSanguophages, "Bloodfiends_SettingReplaceSanguophagesTip".Translate());
            listing.CheckboxLabeled("Bloodfiends_SettingSurviveBrainDestruction".Translate(), ref Settings.regenerationSurvivesBrainDestruction, "Bloodfiends_SettingSurviveBrainDestructionTip".Translate());
            listing.CheckboxLabeled("Bloodfiends_SettingFireHindersRegeneration".Translate(), ref Settings.fireHindersRegeneration, "Bloodfiends_SettingFireHindersRegenerationTip".Translate());
            listing.CheckboxLabeled("Bloodfiends_SettingFleeRain".Translate(), ref Settings.bloodfiendsFleeRain, "Bloodfiends_SettingFleeRainTip".Translate());
            listing.CheckboxLabeled("Bloodfiends_SettingLaSangreHitsAllies".Translate(), ref Settings.laSangreHitsAllies, "Bloodfiends_SettingLaSangreHitsAlliesTip".Translate());
            listing.CheckboxLabeled("Bloodfiends_SettingPrimogenitorDrops".Translate(), ref Settings.firstKindredDropsPrimogenitorBlood, "Bloodfiends_SettingPrimogenitorDropsTip".Translate());

            listing.GapLine();
            listing.Label("Bloodfiends_SettingKindredPerBloodfiend".Translate(Settings.maximumKindredsPerBloodfiend), tooltip: "Bloodfiends_SettingKindredPerBloodfiendTip".Translate());
            Settings.maximumKindredsPerBloodfiend = Mathf.RoundToInt(listing.Slider(Settings.maximumKindredsPerBloodfiend, MinimumKindredsPerBloodfiend, MaximumKindredsSliderValue));

            listing.CheckboxLabeled("Bloodfiends_SettingKindredSlotsFreeOnDeath".Translate(), ref Settings.kindredSlotsFreeOnDeath, "Bloodfiends_SettingKindredSlotsFreeOnDeathTip".Translate());

            if (BloodbagUtility.BloodbagDef != null) DoBloodbagWorkTypes(listing);

            listing.GapLine();
            if (listing.ButtonText("Bloodfiends_SettingResetToDefaults".Translate())) Settings.ResetToDefaults();

            listing.End();
            settingsContentHeight = listing.CurHeight;
            Widgets.EndScrollView();
        }

        private static void DoBloodbagWorkTypes(Listing_Standard listing)
        {
            listing.GapLine();
            listing.Label("Bloodfiends_SettingBloodbagWork".Translate(), tooltip: "Bloodfiends_SettingBloodbagWorkTip".Translate());
            foreach (WorkTypeDef workType in BloodbagWorkTypes.ChoosableWorkTypes)
            {
                bool allowed = BloodbagWorkTypes.IsAllowed(workType);
                bool wasAllowed = allowed;
                listing.CheckboxLabeled("    " + workType.labelShort.CapitalizeFirst(), ref allowed, workType.description);
                if (allowed != wasAllowed) BloodbagWorkTypes.SetAllowed(workType, allowed);
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            BloodbagWorkTypes.RefreshAllBloodbags();
        }
    }
}