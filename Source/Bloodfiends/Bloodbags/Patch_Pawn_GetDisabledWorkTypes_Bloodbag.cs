using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Bloodbags
{
    public class MutantAllowedWorkExtension : DefModExtension
    {
        public List<WorkTypeDef> allowedWorkTypes = new List<WorkTypeDef>();
    }

    public class BloodbagExtension : DefModExtension
    {
        public float hemogenMaxOffset = 2f;   // added to the hemogen maximum when they turn (1 = 100 hemogen)
        public float hemogenPerDamage = 0.005f;   // hemogen drained per point of melee damage dealt (0.005 = 0.5 hemogen)
        public float hemogenLossPerDay = 0.02f;   // a Bloodbag's own slow thirst (0.02 = 2 hemogen a day)
    }

    public static class BloodbagWorkTypes
    {
        private static readonly string[] AlwaysAllowedWorkTypeNames = { "Patient", "PatientBedRest" };

        public static List<WorkTypeDef> DefaultWorkTypes => BloodbagUtility.BloodbagDef?.GetModExtension<MutantAllowedWorkExtension>()?.allowedWorkTypes ?? new List<WorkTypeDef>();

        /// <summary>
        /// The work types shown as checkboxes in the settings (everything except the always-allowed patient ones).
        /// </summary>
        public static IEnumerable<WorkTypeDef> ChoosableWorkTypes => DefDatabase<WorkTypeDef>.AllDefsListForReading
            .Where(workType => !AlwaysAllowedWorkTypeNames.Contains(workType.defName))
            .OrderByDescending(workType => workType.naturalPriority);

        public static bool IsAllowed(WorkTypeDef workType)
        {
            if (AlwaysAllowedWorkTypeNames.Contains(workType.defName)) return true;
            List<string> chosenWorkTypeNames = BloodfiendsMod.Settings.bloodbagWorkTypeNames;
            return chosenWorkTypeNames == null ? DefaultWorkTypes.Contains(workType) : chosenWorkTypeNames.Contains(workType.defName);
        }

        public static void SetAllowed(WorkTypeDef workType, bool allowed)
        {
            BloodfiendsSettings settings = BloodfiendsMod.Settings;
            if (settings.bloodbagWorkTypeNames == null) settings.bloodbagWorkTypeNames = DefaultWorkTypes.Select(defaultWorkType => defaultWorkType.defName).ToList();
            if (allowed && !settings.bloodbagWorkTypeNames.Contains(workType.defName)) settings.bloodbagWorkTypeNames.Add(workType.defName);
            else if (!allowed) settings.bloodbagWorkTypeNames.Remove(workType.defName);
        }

        public static void RefreshAllBloodbags()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(BloodbagUtility.IsBloodbag)) pawn.Notify_DisabledWorkTypesChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    public static class Patch_Pawn_GetDisabledWorkTypes_Bloodbag
    {
        public static void Postfix(Pawn __instance, List<WorkTypeDef> __result)
        {
            if (__result == null || !__instance.IsMutant) return;
            bool isBloodbag = BloodbagUtility.IsBloodbag(__instance);
            MutantAllowedWorkExtension extension = __instance.mutant.Def.GetModExtension<MutantAllowedWorkExtension>();
            if (extension == null && !isBloodbag) return;
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                bool allowed = isBloodbag ? BloodbagWorkTypes.IsAllowed(workType) : extension.allowedWorkTypes.Contains(workType);
                if (!allowed && !__result.Contains(workType)) __result.Add(workType);
            }
        }
    }
}
