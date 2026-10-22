using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hardblood
{
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming_HardbloodCoating
    {
        private const float CoatingAltitude = 0.0004f;   // just above the weapon

        public static void Postfix(Thing eq, Vector3 drawLoc, float aimAngle, bool __runOriginal)
        {
            if (!__runOriginal) return;
            if (!(eq?.ParentHolder is Pawn_EquipmentTracker equipmentTracker) || equipmentTracker.pawn == null) return;
            if (!equipmentTracker.pawn.health.hediffSet.HasHediff(BloodfiendsDefOf.Bloodfiends_HardbloodGrowths)) return;
            if (!HardbloodGraphics.TryGetCoating(eq, out Material material, out Vector2 drawScale)) return;

            // same angle and mirroring math as vanilla's DrawEquipmentAiming
            float drawAngle = aimAngle - 90f;
            bool flipped = false;
            if (aimAngle > 200f && aimAngle < 340f)
            {
                flipped = true;
                drawAngle -= 180f;
                drawAngle -= eq.def.equippedAngleOffset;
            }
            else
            {
                drawAngle += eq.def.equippedAngleOffset;
            }
            drawAngle %= 360f;

            Mesh mesh = flipped ? MeshPool.plane10Flip : MeshPool.plane10;
            Vector2 drawSize = new Vector2(eq.Graphic.drawSize.x * drawScale.x, eq.Graphic.drawSize.y * drawScale.y);
            Matrix4x4 matrix = Matrix4x4.TRS(drawLoc + new Vector3(0f, CoatingAltitude, 0f), Quaternion.AngleAxis(drawAngle, Vector3.up), new Vector3(drawSize.x, 0f, drawSize.y));
            Graphics.DrawMesh(mesh, matrix, material, 0);
        }
    }
}
