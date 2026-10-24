using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Mantle
{
    [StaticConstructorOnStartup]
    public class Gizmo_MantleStatus : Gizmo
    {
        private static readonly Texture2D FullBarTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.55f, 0.04f, 0.06f));
        private static readonly Texture2D EmptyBarTexture = SolidColorMaterials.NewSolidColorTexture(Color.clear);

        public Hediff_Mantle mantle;

        public Gizmo_MantleStatus() => Order = -100f;

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect innerRect = outerRect.ContractedBy(6f);
            Widgets.DrawWindowBackground(outerRect);

            Rect labelRect = innerRect;
            labelRect.height = outerRect.height / 2f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, mantle.def.LabelCap);

            Rect barRect = innerRect;
            barRect.yMin = innerRect.y + innerRect.height / 2f;
            Widgets.FillableBar(barRect, mantle.ChargeFraction, FullBarTexture, EmptyBarTexture, false);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, $"{Mathf.CeilToInt(mantle.charge)} / {Mathf.CeilToInt(mantle.MaximumCharge)}");
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(innerRect, "Bloodfiends_MantleGizmoTip".Translate());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
