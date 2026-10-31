using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bloodfiends
{
    public interface IExtraHemogenCost
    {
        float ExtraHemogenCost { get; }
    }

    [HarmonyPatch(typeof(GeneGizmo_ResourceHemogen), nameof(GeneGizmo_ResourceHemogen.GizmoOnGUI))]
    public static class Patch_GeneGizmoResourceHemogen_GizmoOnGUI_ExtraCost
    {
        private static Texture2D hemogenCostTexture;

        private static Texture2D HemogenCostTexture => hemogenCostTexture ?? (hemogenCostTexture = (Texture2D)AccessTools.Field(typeof(GeneGizmo_ResourceHemogen), "HemogenCostTex").GetValue(null));

        public static void Postfix(Gene_Resource ___gene, Rect ___barRect)
        {
            if (___gene == null || ___gene.Max <= 0f) return;
            if (!(HoveredGizmo() is Command_Ability command)) return;

            float baseCost = 0f;
            float extraCost = 0f;
            foreach (CompAbilityEffect comp in command.Ability.EffectComps)
            {
                if (comp is CompAbilityEffect_HemogenCost hemogenCost) baseCost += hemogenCost.Props.hemogenCost;
                if (comp is IExtraHemogenCost extra) extraCost += extra.ExtraHemogenCost;
            }
            if (extraCost <= 0f) return;

            Rect bar = ___barRect.ContractedBy(3f);
            float valueShare = ___gene.Value / ___gene.Max;
            float afterBaseShare = Mathf.Min(valueShare, Mathf.Max(0f, ___gene.Value - baseCost) / ___gene.Max);
            float afterAllShare = Mathf.Max(0f, ___gene.Value - baseCost - extraCost) / ___gene.Max;
            if (afterAllShare >= afterBaseShare) return;

            Rect extraRect = bar;
            extraRect.xMin = bar.xMin + bar.width * afterAllShare;
            extraRect.xMax = bar.xMin + bar.width * afterBaseShare;
            GUI.color = new Color(1f, 1f, 1f, Pulse() * 0.7f);
            GenUI.DrawTextureWithMaterial(extraRect, HemogenCostTexture, null);
            GUI.color = Color.white;
        }

        private static System.Func<Gizmo> hoveredGizmoGetter;

        private static Gizmo HoveredGizmo()
        {
            if (hoveredGizmoGetter == null)
            {
                System.Reflection.PropertyInfo mapProperty = AccessTools.Property(AccessTools.TypeByName("RimWorld.MapGizmoUtility"), "LastMouseOverGizmo");
                System.Reflection.PropertyInfo inspectProperty = AccessTools.Property(typeof(MainTabWindow_Inspect), "LastMouseoverGizmo");
                if (mapProperty != null) hoveredGizmoGetter = () => mapProperty.GetValue(null) as Gizmo;
                else if (inspectProperty != null) hoveredGizmoGetter = () => inspectProperty.GetValue(MainButtonDefOf.Inspect.TabWindow) as Gizmo;
                else hoveredGizmoGetter = () => null;
            }
            return hoveredGizmoGetter();
        }

        private static float Pulse()
        {
            float time = Mathf.Repeat(Time.time, 0.85f);
            if (time < 0.1f) return time / 0.1f;
            if (time >= 0.25f) return 1f - (time - 0.25f) / 0.6f;
            return 1f;
        }
    }
}