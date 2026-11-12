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

            GameComponent_BloodfiendFamilies.SyncPromotedPawns(registry.Notify_BloodfiendDied(embracerNode), embracerNode.bloodfiendPawn);
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

        [DebugAction("Bloodfiends", "Log filth here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogFilthHere()
        {
            IntVec3 cell = UI.MouseCell();
            foreach (Thing thing in cell.GetThingList(Find.CurrentMap).Where(thing => thing is Filth))
                Log.Message($"[Bloodfiends] Filth at {cell}: {thing.def.defName} (from mod {thing.def.modContentPack?.PackageId})");
        }

        [DebugAction("Bloodfiends", "Set tier (new Family)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SetTier()
        {
            List<DebugActionNode> tierOptions = new List<DebugActionNode>();
            List<HediffStage> stages = BloodfiendsDefOf.Bloodfiends_Lineage.stages;
            for (int generation = GameComponent_BloodfiendFamilies.ElderGeneration; generation <= stages.Count; generation++)
            {
                int chosenGeneration = generation;
                string tierLabel = stages[generation - 1].label ?? generation.ToString();
                tierOptions.Add(new DebugActionNode($"{chosenGeneration}: {tierLabel}", DebugActionType.ToolMapForPawns)
                {
                    pawnAction = pawn => MoveToNewFamily(pawn, chosenGeneration)
                });
            }
            return tierOptions;
        }

        [DebugAction("Bloodfiends", "Make Kindred of...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeKindredOf(Pawn kindredPawn)
        {
            if (!IsBloodfiend(kindredPawn)) return;
            DebugTools.curTool = new DebugTool("Pick the embracer of " + kindredPawn.LabelShort, () =>
            {
                Pawn embracerPawn = Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Pawn>().FirstOrDefault();
                GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
                BloodfiendFamilyNode embracerNode = registry?.GetNodeForPawn(embracerPawn);
                if (embracerPawn == null || embracerPawn == kindredPawn || embracerNode == null)
                {
                    Messages.Message("Pick a different Bloodfiend that's in a Family.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                SetLineageNode(kindredPawn, registry.ForceKindredNode(embracerNode, kindredPawn));
                DebugTools.curTool = null;
            });
        }

        private static void MoveToNewFamily(Pawn pawn, int generation)
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null || !IsBloodfiend(pawn)) return;

            BloodfiendFamilyNode oldNode = registry.GetNodeForPawn(pawn);
            List<BloodfiendFamilyNode> promotedNodes = registry.Notify_BloodfiendDied(oldNode);
            GameComponent_BloodfiendFamilies.SyncPromotedPawns(promotedNodes);
            SetLineageNode(pawn, registry.CreateFamilyFor(pawn, generation));
        }

        private static void SetLineageNode(Pawn pawn, BloodfiendFamilyNode node)
        {
            Hediff_BloodfiendLineage lineageHediff = pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>();
            if (lineageHediff == null)
            {
                lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, pawn);
                lineageHediff.nodeId = node.nodeId;
                pawn.health.AddHediff(lineageHediff);
                return;
            }
            lineageHediff.nodeId = node.nodeId;
            lineageHediff.SyncFromNode();
        }

        private static bool IsBloodfiend(Pawn pawn)
        {
            if (pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() != null) return true;
            Messages.Message("Not a Bloodfiend (no Dipsia gene).", MessageTypeDefOf.RejectInput, false);
            return false;
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
