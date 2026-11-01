using System.Collections.Generic;
using System.Linq;
using Bloodfiends.Bloodbags;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bloodfiends.Family
{
    public class ITab_Pawn_BloodfiendFamily : ITab
    {
        private const float Margin = 10f;
        private const float SlotWidth = 112f;
        private const float BoxWidth = 102f;
        private const float BoxHeight = 42f;
        private const float BadgeHeight = 14f;
        private const float RowHeight = 74f;
        private const float LineThickness = 2f;
        private const int BloodbagRefreshIntervalTicks = 60;

        private static readonly Color PlayerBoxColor = new Color(0.32f, 0.08f, 0.09f);
        private static readonly Color OutsiderBoxColor = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color UnknownBoxColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color OutlineColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color SelectedOutlineColor = new Color(1f, 0.8f, 0.35f);
        private static readonly Color LineColor = new Color(0.55f, 0.12f, 0.12f);
        private static readonly Color BadgeColor = new Color(0.85f, 0.45f, 0.45f);

        private readonly Dictionary<int, int> leafCountsByNodeId = new Dictionary<int, int>();
        private readonly Dictionary<Pawn, List<Pawn>> bloodbagsByMaster = new Dictionary<Pawn, List<Pawn>>();
        private readonly List<Pawn> masterlessBloodbags = new List<Pawn>();
        private int cachedBloodbagFamilyId = -1;
        private int cachedBloodbagTick = -99999;
        private Vector2 scrollPosition;

        public ITab_Pawn_BloodfiendFamily()
        {
            size = new Vector2(560f, 440f);
            labelKey = "Bloodfiends_FamilyTab";
        }

        private int SelectedFamilyId
        {
            get
            {
                GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
                if (registry == null || SelPawn == null) return -1;

                BloodfiendFamilyNode selectedNode = registry.GetNodeForPawn(SelPawn);
                if (selectedNode != null) return selectedNode.familyId;
                if (!BloodbagUtility.IsBloodbag(SelPawn)) return -1;

                int bloodbagFamilyId = BloodbagUtility.FamilyOf(SelPawn);
                return registry.NodesOfFamily(bloodbagFamilyId).Any() ? bloodbagFamilyId : -1;
            }
        }

        public override bool IsVisible => SelectedFamilyId >= 0;

        protected override void FillTab()
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            int familyId = SelectedFamilyId;
            if (registry == null || familyId < 0) return;

            bool selectedIsBloodbag = BloodbagUtility.IsBloodbag(SelPawn);
            Pawn selectedMaster = selectedIsBloodbag ? BloodbagUtility.MasterOf(SelPawn) : null;
            BloodfiendFamilyNode highlightedNode = selectedIsBloodbag ? registry.GetNodeForPawn(selectedMaster) : registry.GetNodeForPawn(SelPawn);
            RefreshBloodbags(familyId);

            List<BloodfiendFamilyNode> familyNodes = registry.NodesOfFamily(familyId).ToList();
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
            Widgets.Label(titleRect, TitleFor(selectedIsBloodbag, selectedMaster, familyNodes.Count));
            Text.Font = GameFont.Small;

            Rect treeOutRect = new Rect(contentRect.x, titleRect.yMax + 6f, contentRect.width, contentRect.height - titleRect.height - 6f);
            float treeWidth = totalLeafCount * SlotWidth;
            Rect treeViewRect = new Rect(0f, 0f, Mathf.Max(treeOutRect.width - 16f, treeWidth), (bottomGeneration - topGeneration + 1) * RowHeight);

            Widgets.BeginScrollView(treeOutRect, ref scrollPosition, treeViewRect);
            float leftEdge = (treeViewRect.width - treeWidth) / 2f;
            foreach (BloodfiendFamilyNode rootNode in rootNodes)
            {
                DrawSubtree(registry, rootNode, leftEdge, topGeneration, highlightedNode);
                leftEdge += leafCountsByNodeId[rootNode.nodeId] * SlotWidth;
            }
            Widgets.EndScrollView();
        }

        private string TitleFor(bool selectedIsBloodbag, Pawn selectedMaster, int memberCount)
        {
            string familyTitle = "Bloodfiends_FamilyTabTitle".Translate(memberCount).Resolve();
            if (selectedIsBloodbag) familyTitle = (selectedMaster != null && !selectedMaster.Dead ? "Bloodfiends_FamilyTabBloodbagOf".Translate(selectedMaster.Named("MASTER")).Resolve() : "Bloodfiends_FamilyTabMasterlessBloodbag".Translate().Resolve()) + " · " + familyTitle;
            if (masterlessBloodbags.Count > 0) familyTitle += " · " + "Bloodfiends_FamilyTabMasterlessCount".Translate(masterlessBloodbags.Count).Resolve();
            return familyTitle;
        }

        private void RefreshBloodbags(int familyId)
        {
            int currentTick = Find.TickManager.TicksGame;
            if (familyId == cachedBloodbagFamilyId && currentTick - cachedBloodbagTick < BloodbagRefreshIntervalTicks) return;
            cachedBloodbagFamilyId = familyId;
            cachedBloodbagTick = currentTick;

            bloodbagsByMaster.Clear();
            masterlessBloodbags.Clear();
            IEnumerable<Pawn> familyBloodbags = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                .Where(pawn => BloodbagUtility.IsBloodbag(pawn) && BloodbagUtility.FamilyOf(pawn) == familyId);

            foreach (Pawn bloodbag in familyBloodbags)
            {
                Pawn master = BloodbagUtility.MasterOf(bloodbag);
                if (master == null || master.Dead)
                {
                    masterlessBloodbags.Add(bloodbag);
                    continue;
                }
                if (!bloodbagsByMaster.TryGetValue(master, out List<Pawn> masterBloodbags)) bloodbagsByMaster[master] = masterBloodbags = new List<Pawn>();
                masterBloodbags.Add(bloodbag);
            }
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

        private List<Pawn> BloodbagsOf(BloodfiendFamilyNode node) => node.HasLivingPawn && bloodbagsByMaster.TryGetValue(node.bloodfiendPawn, out List<Pawn> masterBloodbags) ? masterBloodbags : null;

        private Rect DrawSubtree(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node, float leftEdge, int topGeneration, BloodfiendFamilyNode highlightedNode)
        {
            float subtreeWidth = leafCountsByNodeId[node.nodeId] * SlotWidth;
            float boxTop = (node.generationNumber - topGeneration) * RowHeight + (RowHeight - BoxHeight) / 2f;
            Rect boxRect = new Rect(leftEdge + (subtreeWidth - BoxWidth) / 2f, boxTop, BoxWidth, BoxHeight);
            List<Pawn> nodeBloodbags = BloodbagsOf(node);
            float lineStartY = nodeBloodbags != null ? boxRect.yMax + BadgeHeight : boxRect.yMax;

            float kindredLeftEdge = leftEdge;
            foreach (BloodfiendFamilyNode kindredNode in KindredOf(registry, node))
            {
                Rect kindredRect = DrawSubtree(registry, kindredNode, kindredLeftEdge, topGeneration, highlightedNode);
                Widgets.DrawLine(new Vector2(boxRect.center.x, lineStartY), new Vector2(kindredRect.center.x, kindredRect.y), LineColor, LineThickness);
                kindredLeftEdge += leafCountsByNodeId[kindredNode.nodeId] * SlotWidth;
            }

            DrawNodeBox(boxRect, node, node == highlightedNode);
            if (nodeBloodbags != null) DrawBloodbagBadge(boxRect, nodeBloodbags);
            return boxRect;
        }

        private static void DrawNodeBox(Rect boxRect, BloodfiendFamilyNode node, bool isHighlighted)
        {
            Pawn memberPawn = node.HasLivingPawn ? node.bloodfiendPawn : null;
            Color boxColor = memberPawn == null ? UnknownBoxColor : memberPawn.Faction == Faction.OfPlayer ? PlayerBoxColor : OutsiderBoxColor;
            Widgets.DrawBoxSolidWithOutline(boxRect, boxColor, isHighlighted ? SelectedOutlineColor : OutlineColor, isHighlighted ? 2 : 1);

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

        private static void DrawBloodbagBadge(Rect boxRect, List<Pawn> nodeBloodbags)
        {
            Rect badgeRect = new Rect(boxRect.x, boxRect.yMax, boxRect.width, BadgeHeight);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = BadgeColor;
            Widgets.Label(badgeRect, "Bloodfiends_FamilyTabBloodbags".Translate(nodeBloodbags.Count));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Widgets.DrawHighlightIfMouseover(badgeRect);
            TooltipHandler.TipRegion(badgeRect, string.Join("\n", nodeBloodbags.Select(bloodbag => bloodbag.LabelShortCap)));

            Pawn spawnedBloodbag = nodeBloodbags.FirstOrDefault(bloodbag => bloodbag.SpawnedOrAnyParentSpawned);
            if (Widgets.ButtonInvisible(badgeRect) && spawnedBloodbag != null) CameraJumper.TryJumpAndSelect(spawnedBloodbag);
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