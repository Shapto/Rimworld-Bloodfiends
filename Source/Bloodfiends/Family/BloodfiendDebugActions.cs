using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public static class BloodfiendDebugActions
    {
        [DebugAction("Bloodfiends", "Kill embracer", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void KillEmbracer(Pawn pawn)
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode embracerNode = registry?.GetNode(pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node?.embracerNodeId ?? -1);
            if (embracerNode == null)
            {
                Messages.Message("No embracer: this Bloodfiend is the root of its Family.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (embracerNode.HasLivingPawn)
            {
                embracerNode.bloodfiendPawn.Kill(null);
                return;
            }

            GameComponent_BloodfiendFamilies.SyncPromotedPawns(registry.Notify_BloodfiendDied(embracerNode));
        }

        [DebugAction("Bloodfiends", "Log family tree", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogFamilyTree(Pawn pawn)
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode node = pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node;
            if (registry == null || node == null) return;

            BloodfiendFamilyNode rootNode = node;
            while (registry.GetNode(rootNode.embracerNodeId) is BloodfiendFamilyNode embracerNode) rootNode = embracerNode;

            StringBuilder builder = new StringBuilder($"Family {node.familyId}:\n");
            AppendNode(registry, rootNode, 0, builder);
            Log.Message(builder.ToString());
        }

        private static void AppendNode(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node, int depth, StringBuilder builder)
        {
            string pawnLabel = node.bloodfiendPawn == null ? "(offscreen)" : node.bloodfiendPawn.LabelShort;
            builder.AppendLine($"{new string(' ', depth * 2)}- [{node.generationNumber}] {pawnLabel} (node {node.nodeId}, {node.kindredCreatedCount} embraced)");

            foreach (int childNodeId in node.embracedNodeIds)
            {
                BloodfiendFamilyNode childNode = registry.GetNode(childNodeId);
                if (childNode != null) AppendNode(registry, childNode, depth + 1, builder);
            }
        }
    }
}
