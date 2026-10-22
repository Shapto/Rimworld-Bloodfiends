using LudeonTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Hardblood
{
    public static class HardbloodDebugActions
    {
        [DebugAction("Bloodfiends", "Grow hardblood (Bloodbag)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrowHardblood(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(BloodfiendsDefOf.Bloodfiends_HardbloodGrowths)) return;
            pawn.health.AddHediff(BloodfiendsDefOf.Bloodfiends_HardbloodGrowths);
        }

        [DebugAction("Bloodfiends", "Remove hardblood (Bloodbag)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RemoveHardblood(Pawn pawn)
        {
            Hediff growths = pawn.health.hediffSet.GetFirstHediffOfDef(BloodfiendsDefOf.Bloodfiends_HardbloodGrowths);
            if (growths != null) pawn.health.RemoveHediff(growths);
        }

        /// <summary>
        /// Throws the pawn's growths away and builds them again (after changing the code or the crystals).
        /// </summary>
        [DebugAction("Bloodfiends", "Rebuild hardblood", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RebuildHardblood(Pawn pawn)
        {
            HardbloodGraphics.Forget(pawn);
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }
}
