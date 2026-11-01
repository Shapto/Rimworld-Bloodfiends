using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bloodfiends.Family
{
    /// <summary>
    /// Inspect tab drawing the selected Bloodfiend's Family tree. Only visible on pawns that belong to a Family.
    /// </summary>
    public class ITab_Pawn_BloodfiendFamily : ITab
    {
        private const float Margin = 10f;
        private const float SlotWidth = 112f;
        private const float BoxWidth = 102f;
        private const float BoxHeight = 42f;
        private const float RowHeight = 74f;
        private const float LineThickness = 2f;

        private static readonly Color PlayerBoxColor = new Color(0.32f, 0.08f, 0.09f);
        private static readonly Color OutsiderBoxColor = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color UnknownBoxColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color OutlineColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color SelectedOutlineColor = new Color(1f, 0.8f, 0.35f);
        private static readonly Color LineColor = new Color(0.55f, 0.12f, 0.12f);

        private readonly Dictionary<int, int> leafCountsByNodeId = new Dictionary<int, int>();
        private Vector2 scrollPosition;

        public ITab_Pawn_BloodfiendFamily()
        {
            size = new Vector2(560f, 440f);
            labelKey = "Bloodfiends_FamilyTab";
        }

        private BloodfiendFamilyNode SelectedNode => GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(SelPawn);

        public override bool IsVisible => SelectedNode != null;

        protected override void FillTab()
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode selectedNode = SelectedNode;
            if (registry == null || selectedNode == null) return;

            List<BloodfiendFamilyNode> familyNodes = registry.NodesOfFamily(selectedNode.familyId).ToList();
            List<BloodfiendFamilyNode> rootNodes = familyNodes
                .Where(node => registry.GetNode(node.embracerNodeId) == null)
                .OrderBy(node => node.generationNumber)
                .ToList();
            int topGeneration = familyNodes.Min(node => node.generationNumber);
            int bottomGeneration = familyNodes.Max(node => node.generationNumber);

            leafCountsByNodeId.Clear();
            int totalLeafCount = rootNodes.Sum(rootNode => CountLeaves(registry, rootNode));

            Rect contentRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, Text.LineHeight);
            Widgets.Label(titleRect, "Bloodfiends_FamilyTabTitle".Translate(familyNodes.Count));
            Text.Font = GameFont.Small;

            Rect treeOutRect = new Rect(contentRect.x, titleRect.yMax + 6f, contentRect.width, contentRect.height - titleRect.height - 6f);
            float treeWidth = totalLeafCount * SlotWidth;
            Rect treeViewRect = new Rect(0f, 0f, Mathf.Max(treeOutRect.width - 16f, treeWidth), (bottomGeneration - topGeneration + 1) * RowHeight);

            Widgets.BeginScrollView(treeOutRect, ref scrollPosition, treeViewRect);
            float leftEdge = (treeViewRect.width - treeWidth) / 2f;
            foreach (BloodfiendFamilyNode rootNode in rootNodes)
            {
                DrawSubtree(registry, rootNode, leftEdge, topGeneration, selectedNode);
                leftEdge += leafCountsByNodeId[rootNode.nodeId] * SlotWidth;
            }
            Widgets.EndScrollView();
        }

        private int CountLeaves(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node)
        {
            if (leafCountsByNodeId.TryGetValue(node.nodeId, out int cachedLeafCount)) return cachedLeafCount;

            int leafCount = Mathf.Max(1, KindredOf(registry, node).Sum(kindredNode => CountLeaves(registry, kindredNode)));
            leafCountsByNodeId[node.nodeId] = leafCount;
            return leafCount;
        }

        private static IEnumerable<BloodfiendFamilyNode> KindredOf(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node) => node.embracedNodeIds
            .Select(registry.GetNode)
            .Where(kindredNode => kindredNode != null);

        private Rect DrawSubtree(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node, float leftEdge, int topGeneration, BloodfiendFamilyNode selectedNode)
        {
            float subtreeWidth = leafCountsByNodeId[node.nodeId] * SlotWidth;
            float boxTop = (node.generationNumber - topGeneration) * RowHeight + (RowHeight - BoxHeight) / 2f;
            Rect boxRect = new Rect(leftEdge + (subtreeWidth - BoxWidth) / 2f, boxTop, BoxWidth, BoxHeight);

            float kindredLeftEdge = leftEdge;
            foreach (BloodfiendFamilyNode kindredNode in KindredOf(registry, node))
            {
                Rect kindredRect = DrawSubtree(registry, kindredNode, kindredLeftEdge, topGeneration, selectedNode);
                Widgets.DrawLine(new Vector2(boxRect.center.x, boxRect.yMax), new Vector2(kindredRect.center.x, kindredRect.y), LineColor, LineThickness);
                kindredLeftEdge += leafCountsByNodeId[kindredNode.nodeId] * SlotWidth;
            }

            DrawNodeBox(boxRect, node, node == selectedNode);
            return boxRect;
        }

        private static void DrawNodeBox(Rect boxRect, BloodfiendFamilyNode node, bool isSelected)
        {
            Pawn memberPawn = node.HasLivingPawn ? node.bloodfiendPawn : null;
            Color boxColor = memberPawn == null ? UnknownBoxColor : memberPawn.Faction == Faction.OfPlayer ? PlayerBoxColor : OutsiderBoxColor;
            Widgets.DrawBoxSolidWithOutline(boxRect, boxColor, isSelected ? SelectedOutlineColor : OutlineColor, isSelected ? 2 : 1);

            string nameLabel = memberPawn != null ? memberPawn.LabelShortCap : "Bloodfiends_FamilyTabUnknown".Translate().Resolve();
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(boxRect.TopHalf(), nameLabel.Truncate(boxRect.width - 6f));
            GUI.color = Color.gray;
            Widgets.Label(boxRect.BottomHalf(), TierLabelForGeneration(node.generationNumber));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (memberPawn == null)
            {
                TooltipHandler.TipRegion(boxRect, "Bloodfiends_FamilyTabUnknownTip".Translate());
                return;
            }

            Widgets.DrawHighlightIfMouseover(boxRect);
            TooltipHandler.TipRegion(boxRect, "Bloodfiends_FamilyTabMemberTip".Translate(memberPawn.Named("PAWN"), (memberPawn.Faction?.Name ?? "Bloodfiends_FamilyTabNoFaction".Translate().Resolve()).Named("FACTION")));
            if (Widgets.ButtonInvisible(boxRect) && memberPawn.SpawnedOrAnyParentSpawned) CameraJumper.TryJumpAndSelect(memberPawn);
        }

        private static string TierLabelForGeneration(int generation) => BloodfiendsDefOf.Bloodfiends_Lineage.stages?
            .LastOrDefault(stage => stage.minSeverity <= generation)?
            .label?.CapitalizeFirst() ?? "";
    }

    [StaticConstructorOnStartup]
    public static class BloodfiendFamilyTabInjector
    {
        static BloodfiendFamilyTabInjector()
        {
            InspectTabBase familyTab = InspectTabManager.GetSharedInstance(typeof(ITab_Pawn_BloodfiendFamily));
            foreach (ThingDef raceDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(thingDef => thingDef.race?.Humanlike == true))
            {
                if (raceDef.inspectorTabsResolved == null || raceDef.inspectorTabsResolved.Contains(familyTab)) continue;
                raceDef.inspectorTabs?.Add(typeof(ITab_Pawn_BloodfiendFamily));
                raceDef.inspectorTabsResolved.Add(familyTab);
            }
        }
    }
}