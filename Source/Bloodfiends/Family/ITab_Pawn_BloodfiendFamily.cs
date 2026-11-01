using System;
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
        private const float HeaderHeight = 46f;
        private const float RailWidth = 118f;
        private const float MaximumPortraitRadius = 34f;
        private const float MinimumPortraitRadius = 16f;
        private const float PortraitRadiusStepPerGeneration = 5f;
        private const float NamedPortraitMinimumRadius = 19f;
        private const float NodeSpacing = 14f;
        private const float NameSpace = 16f;
        private const float RowPadding = 22f;
        private const float TreeSidePadding = 16f;
        private const float BadgeRadius = 9f;
        private const float TieThickness = 2f;
        private const float TieGapFromRing = 3f;
        private const float ScrollbarWidth = 16f;
        private const float CrownWidth = 30f;
        private const float CrownHeight = 18f;
        private const float CrownOverlap = 4f;
        private const int BloodbagRefreshIntervalTicks = 60;

        private static readonly Vector3 PortraitCameraOffset = new Vector3(0f, 0f, 0.3f);
        private const float PortraitCameraZoom = 1.28f;

        private static readonly Color RailColor = new Color(0.137f, 0.141f, 0.145f);
        private static readonly Color DividerColor = new Color(0.27f, 0.27f, 0.27f);
        private static readonly Color BandDividerColor = new Color(0.185f, 0.19f, 0.195f);
        private static readonly Color PortraitBackgroundColor = new Color(0.17f, 0.17f, 0.17f);
        private static readonly Color UnknownDiscColor = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color UnknownMarkColor = new Color(0.29f, 0.29f, 0.29f);
        private static readonly Color PlayerRingColor = new Color(0.7f, 0.16f, 0.16f);
        private static readonly Color OutsiderRingColor = new Color(0.48f, 0.48f, 0.48f);
        private static readonly Color UnknownRingColor = new Color(0.23f, 0.23f, 0.23f);
        private static readonly Color HighlightedRingColor = new Color(0.88f, 0.71f, 0.35f);
        private static readonly Color TieColor = new Color(0.56f, 0.11f, 0.11f);
        private static readonly Color TitleColor = new Color(0.91f, 0.85f, 0.71f);
        private static readonly Color NameColor = new Color(0.84f, 0.84f, 0.84f);
        private static readonly Color NameBackdropColor = new Color(0.114f, 0.118f, 0.122f, 0.85f);

        private readonly Dictionary<int, float> subtreeWidthsByNodeId = new Dictionary<int, float>();
        private readonly Dictionary<int, Vector2> centersByNodeId = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> bandTopsByGeneration = new Dictionary<int, float>();
        private readonly Dictionary<int, float> bandHeightsByGeneration = new Dictionary<int, float>();
        private readonly Dictionary<Pawn, List<Pawn>> bloodbagsByMaster = new Dictionary<Pawn, List<Pawn>>();
        private readonly List<Pawn> masterlessBloodbags = new List<Pawn>();
        private int cachedBloodbagFamilyId = -1;
        private int cachedBloodbagTick = -99999;
        private float treeLeftEdge;
        private Vector2 scrollPosition;

        public ITab_Pawn_BloodfiendFamily()
        {
            size = new Vector2(640f, 560f);
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
            LayOutTree(registry, rootNodes, topGeneration, bottomGeneration, out float treeWidth, out float treeHeight);

            Rect contentRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            DrawHeader(new Rect(contentRect.x, contentRect.y, contentRect.width, HeaderHeight), familyNodes, selectedIsBloodbag, selectedMaster);

            Rect treeOutRect = new Rect(contentRect.x, contentRect.y + HeaderHeight, contentRect.width, contentRect.height - HeaderHeight);
            Widgets.DrawBoxSolidWithOutline(treeOutRect, FamilyTreeTextures.TreeBackgroundColor, DividerColor);
            float contentWidth = RailWidth + treeWidth;
            bool needsVerticalScrollbar = treeHeight > treeOutRect.height || treeHeight > treeOutRect.height - ScrollbarWidth && contentWidth > treeOutRect.width;
            bool needsHorizontalScrollbar = contentWidth > treeOutRect.width - (needsVerticalScrollbar ? ScrollbarWidth : 0f);
            float viewWidth = Mathf.Max(treeOutRect.width - (needsVerticalScrollbar ? ScrollbarWidth : 0f), contentWidth);
            float viewHeight = Mathf.Max(treeOutRect.height - (needsHorizontalScrollbar ? ScrollbarWidth : 0f), treeHeight);
            Rect treeViewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            treeLeftEdge = RailWidth + (treeViewRect.width - RailWidth - treeWidth) / 2f;

            Widgets.BeginScrollView(treeOutRect, ref scrollPosition, treeViewRect);
            DrawBandDividers(treeViewRect.width, topGeneration, bottomGeneration);
            foreach (BloodfiendFamilyNode node in familyNodes) DrawPortrait(node, node == highlightedNode);
            foreach (BloodfiendFamilyNode node in familyNodes) DrawTies(registry, node);
            foreach (BloodfiendFamilyNode node in familyNodes) DrawOverlays(node);
            DrawRail(topGeneration, bottomGeneration, treeViewRect.height);
            Widgets.EndScrollView();
        }

        private static float PortraitRadiusFor(int generation) => Mathf.Max(MinimumPortraitRadius, MaximumPortraitRadius - (generation - 1) * PortraitRadiusStepPerGeneration);

        private Vector2 CenterOf(BloodfiendFamilyNode node) => centersByNodeId[node.nodeId] + new Vector2(treeLeftEdge, 0f);

        private static IEnumerable<BloodfiendFamilyNode> KindredOf(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node) => node.embracedNodeIds
            .Select(registry.GetNode)
            .Where(kindredNode => kindredNode != null);

        private List<Pawn> BloodbagsOf(BloodfiendFamilyNode node) => node.HasLivingPawn && bloodbagsByMaster.TryGetValue(node.bloodfiendPawn, out List<Pawn> masterBloodbags) ? masterBloodbags : null;

        private void LayOutTree(GameComponent_BloodfiendFamilies registry, List<BloodfiendFamilyNode> rootNodes, int topGeneration, int bottomGeneration, out float treeWidth, out float treeHeight)
        {
            subtreeWidthsByNodeId.Clear();
            centersByNodeId.Clear();
            bandTopsByGeneration.Clear();
            bandHeightsByGeneration.Clear();

            float bandTop = 0f;
            for (int generation = topGeneration; generation <= bottomGeneration; generation++)
            {
                float bandHeight = PortraitRadiusFor(generation) * 2f + NameSpace + RowPadding;
                bandTopsByGeneration[generation] = bandTop;
                bandHeightsByGeneration[generation] = bandHeight;
                bandTop += bandHeight;
            }
            treeHeight = bandTop;

            float leftEdge = TreeSidePadding;
            foreach (BloodfiendFamilyNode rootNode in rootNodes)
            {
                PlaceSubtree(registry, rootNode, leftEdge);
                leftEdge += SubtreeWidth(registry, rootNode);
            }
            treeWidth = leftEdge + TreeSidePadding;
        }
        private float SubtreeWidth(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node)
        {
            if (subtreeWidthsByNodeId.TryGetValue(node.nodeId, out float cachedWidth)) return cachedWidth;

            float ownWidth = PortraitRadiusFor(node.generationNumber) * 2f + NodeSpacing;
            float subtreeWidth = Mathf.Max(ownWidth, KindredOf(registry, node).Sum(kindredNode => SubtreeWidth(registry, kindredNode)));
            subtreeWidthsByNodeId[node.nodeId] = subtreeWidth;
            return subtreeWidth;
        }

        private void PlaceSubtree(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node, float leftEdge)
        {
            float subtreeWidth = SubtreeWidth(registry, node);
            float rowCenterY = bandTopsByGeneration[node.generationNumber] + RowPadding / 2f + PortraitRadiusFor(node.generationNumber);
            centersByNodeId[node.nodeId] = new Vector2(leftEdge + subtreeWidth / 2f, rowCenterY);

            List<BloodfiendFamilyNode> kindredNodes = KindredOf(registry, node).ToList();
            float kindredWidth = kindredNodes.Sum(kindredNode => SubtreeWidth(registry, kindredNode));
            float kindredLeftEdge = leftEdge + (subtreeWidth - kindredWidth) / 2f;
            foreach (BloodfiendFamilyNode kindredNode in kindredNodes)
            {
                PlaceSubtree(registry, kindredNode, kindredLeftEdge);
                kindredLeftEdge += SubtreeWidth(registry, kindredNode);
            }
        }

        private void DrawHeader(Rect headerRect, List<BloodfiendFamilyNode> familyNodes, bool selectedIsBloodbag, Pawn selectedMaster)
        {
            Pawn eldestKnownMember = familyNodes
                .Where(node => node.HasLivingPawn)
                .OrderBy(node => node.generationNumber)
                .Select(node => node.bloodfiendPawn)
                .FirstOrDefault();
            string title = eldestKnownMember != null ? "Bloodfiends_FamilyTabBloodlineOf".Translate(eldestKnownMember.Named("PAWN")).Resolve() : "Bloodfiends_FamilyTabUnknownBloodline".Translate().Resolve();

            int metCount = familyNodes.Count(node => node.HasLivingPawn);
            int bloodbagCount = bloodbagsByMaster.Values.Sum(masterBloodbags => masterBloodbags.Count) + masterlessBloodbags.Count;
            string summary = "Bloodfiends_FamilyTabSummary".Translate(familyNodes.Count, metCount, bloodbagCount).Resolve();
            if (masterlessBloodbags.Count > 0) summary += " · " + "Bloodfiends_FamilyTabMasterlessCount".Translate(masterlessBloodbags.Count).Resolve();
            if (selectedIsBloodbag) summary = (selectedMaster != null && !selectedMaster.Dead ? "Bloodfiends_FamilyTabBloodbagOf".Translate(selectedMaster.Named("MASTER")).Resolve() : "Bloodfiends_FamilyTabMasterlessBloodbag".Translate().Resolve()) + " · " + summary;

            Text.Font = GameFont.Medium;
            GUI.color = TitleColor;
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width, 28f), title);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(headerRect.x, headerRect.y + 26f, headerRect.width, 18f), summary);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawBandDividers(float viewWidth, int topGeneration, int bottomGeneration)
        {
            GUI.color = BandDividerColor;
            for (int generation = topGeneration + 1; generation <= bottomGeneration; generation++) Widgets.DrawLineHorizontal(0f, bandTopsByGeneration[generation], viewWidth);
            GUI.color = Color.white;
        }

        private void DrawRail(int topGeneration, int bottomGeneration, float viewHeight)
        {
            float railLeft = scrollPosition.x;
            Widgets.DrawBoxSolid(new Rect(railLeft, 0f, RailWidth, viewHeight), RailColor);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            for (int generation = topGeneration; generation <= bottomGeneration; generation++)
            {
                Rect labelRect = new Rect(railLeft + 10f, bandTopsByGeneration[generation], RailWidth - 14f, bandHeightsByGeneration[generation]);
                Widgets.Label(labelRect, TierLabelForGeneration(generation));
            }

            GUI.color = DividerColor;
            Widgets.DrawLineVertical(railLeft + RailWidth, 0f, viewHeight);
            for (int generation = topGeneration + 1; generation <= bottomGeneration; generation++) Widgets.DrawLineHorizontal(railLeft, bandTopsByGeneration[generation], RailWidth);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawPortrait(BloodfiendFamilyNode node, bool isHighlighted)
        {
            Vector2 center = CenterOf(node);
            float radius = PortraitRadiusFor(node.generationNumber);
            Rect portraitRect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            Pawn memberPawn = node.HasLivingPawn ? node.bloodfiendPawn : null;

            if (memberPawn != null)
            {
                DrawTinted(portraitRect, FamilyTreeTextures.Disc, PortraitBackgroundColor);
                GUI.DrawTexture(portraitRect, PortraitsCache.Get(memberPawn, portraitRect.size, Rot4.South, PortraitCameraOffset, PortraitCameraZoom, healthStateOverride: PawnHealthState.Mobile));
                GUI.DrawTexture(portraitRect, FamilyTreeTextures.CornerMask);
            }
            else
            {
                DrawTinted(portraitRect, FamilyTreeTextures.Disc, UnknownDiscColor);
                Text.Font = radius >= 24f ? GameFont.Medium : GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UnknownMarkColor;
                Widgets.Label(portraitRect, "?");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            Color ringColor = isHighlighted ? HighlightedRingColor : memberPawn == null ? UnknownRingColor : memberPawn.Faction == Faction.OfPlayer ? PlayerRingColor : OutsiderRingColor;
            if (memberPawn != null && Mouse.IsOver(portraitRect)) ringColor = Color.Lerp(ringColor, Color.white, 0.35f);
            DrawTinted(portraitRect, isHighlighted ? FamilyTreeTextures.ThickRing : FamilyTreeTextures.Ring, ringColor);
        }

        private void DrawTies(GameComponent_BloodfiendFamilies registry, BloodfiendFamilyNode node)
        {
            List<BloodfiendFamilyNode> kindredNodes = KindredOf(registry, node).ToList();
            if (kindredNodes.Count == 0) return;

            Vector2 embracerCenter = CenterOf(node);
            float embracerRadius = PortraitRadiusFor(node.generationNumber);
            bool embracerHasName = node.HasLivingPawn && embracerRadius >= NamedPortraitMinimumRadius;
            float tieTop = embracerCenter.y + embracerRadius + (embracerHasName ? NameSpace + 2f : 0f) + TieGapFromRing;

            float kindredTop = kindredNodes.Min(kindredNode => CenterOf(kindredNode).y - PortraitRadiusFor(kindredNode.generationNumber)) - TieGapFromRing;
            float barY = Mathf.Round((tieTop + kindredTop) / 2f);
            float barLeft = Mathf.Min(embracerCenter.x, kindredNodes.Min(kindredNode => CenterOf(kindredNode).x));
            float barRight = Mathf.Max(embracerCenter.x, kindredNodes.Max(kindredNode => CenterOf(kindredNode).x));

            DrawVerticalTie(embracerCenter.x, tieTop, barY);
            DrawHorizontalTie(barLeft, barRight, barY);
            foreach (BloodfiendFamilyNode kindredNode in kindredNodes)
            {
                Vector2 kindredCenter = CenterOf(kindredNode);
                DrawVerticalTie(kindredCenter.x, barY, kindredCenter.y - PortraitRadiusFor(kindredNode.generationNumber) - TieGapFromRing);
            }
        }

        private static void DrawVerticalTie(float x, float top, float bottom)
        {
            if (bottom <= top) return;
            Widgets.DrawBoxSolid(new Rect(Mathf.Round(x - TieThickness / 2f), Mathf.Round(top), TieThickness, Mathf.Round(bottom - top)), TieColor);
        }

        private static void DrawHorizontalTie(float left, float right, float y)
        {
            if (right <= left) return;
            Widgets.DrawBoxSolid(new Rect(Mathf.Round(left), Mathf.Round(y - TieThickness / 2f), Mathf.Round(right - left) + TieThickness, TieThickness), TieColor);
        }

        private void DrawOverlays(BloodfiendFamilyNode node)
        {
            Vector2 center = CenterOf(node);
            float radius = PortraitRadiusFor(node.generationNumber);
            Rect portraitRect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            Pawn memberPawn = node.HasLivingPawn ? node.bloodfiendPawn : null;

            if (node.generationNumber == GameComponent_BloodfiendFamilies.ElderGeneration) GUI.DrawTexture(new Rect(center.x - CrownWidth / 2f, portraitRect.y - CrownHeight + CrownOverlap, CrownWidth, CrownHeight), FamilyTreeTextures.Crown);
            if (memberPawn != null && radius >= NamedPortraitMinimumRadius) DrawName(center, radius, memberPawn.LabelShortCap);

            List<Pawn> nodeBloodbags = BloodbagsOf(node);
            if (nodeBloodbags != null) DrawBloodbagBadge(center, radius, nodeBloodbags);

            if (memberPawn == null)
            {
                TooltipHandler.TipRegion(portraitRect, "Bloodfiends_FamilyTabUnknownTip".Translate());
                return;
            }

            TooltipHandler.TipRegion(portraitRect, "Bloodfiends_FamilyTabMemberTip".Translate(memberPawn.Named("PAWN"), (memberPawn.Faction?.Name ?? "Bloodfiends_FamilyTabNoFaction".Translate().Resolve()).Named("FACTION")));
            if (Widgets.ButtonInvisible(portraitRect) && memberPawn.SpawnedOrAnyParentSpawned) CameraJumper.TryJumpAndSelect(memberPawn);
        }

        private static void DrawName(Vector2 center, float radius, string nameLabel)
        {
            Text.Font = GameFont.Tiny;
            float nameWidth = Text.CalcSize(nameLabel).x + 6f;
            Rect nameRect = new Rect(center.x - nameWidth / 2f, center.y + radius + 2f, nameWidth, NameSpace);
            Widgets.DrawBoxSolid(nameRect, NameBackdropColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = NameColor;
            Widgets.Label(nameRect, nameLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawBloodbagBadge(Vector2 center, float radius, List<Pawn> nodeBloodbags)
        {
            Vector2 badgeCenter = center + new Vector2(radius * 0.72f, radius * 0.62f);
            Rect badgeRect = new Rect(badgeCenter.x - BadgeRadius, badgeCenter.y - BadgeRadius, BadgeRadius * 2f, BadgeRadius * 2f);
            DrawTinted(badgeRect, FamilyTreeTextures.Disc, FamilyTreeTextures.TreeBackgroundColor);
            DrawTinted(badgeRect, FamilyTreeTextures.Ring, PlayerRingColor);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(badgeRect, nodeBloodbags.Count.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            string bloodbagNames = string.Join("\n", nodeBloodbags.Select(bloodbag => bloodbag.LabelShortCap));
            TooltipHandler.TipRegion(badgeRect, "Bloodfiends_FamilyTabBloodbags".Translate(nodeBloodbags.Count) + "\n\n" + bloodbagNames);

            Pawn spawnedBloodbag = nodeBloodbags.FirstOrDefault(bloodbag => bloodbag.SpawnedOrAnyParentSpawned);
            if (Widgets.ButtonInvisible(badgeRect) && spawnedBloodbag != null) CameraJumper.TryJumpAndSelect(spawnedBloodbag);
        }

        private static void DrawTinted(Rect rect, Texture2D texture, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = Color.white;
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

        private static string TierLabelForGeneration(int generation) => BloodfiendsDefOf.Bloodfiends_Lineage.stages?
            .LastOrDefault(stage => stage.minSeverity <= generation)?
            .label?.CapitalizeFirst() ?? "";
    }

    [StaticConstructorOnStartup]
    public static class FamilyTreeTextures
    {
        private const int CircleTextureSize = 128;
        private const float PixelSize = 2f / CircleTextureSize;
        private const float RingThickness = 0.09f;
        private const float ThickRingThickness = 0.16f;
        private const int CrownTextureWidth = 64;
        private const int CrownTextureHeight = 40;

        public static readonly Color TreeBackgroundColor = new Color(0.114f, 0.118f, 0.122f);
        private static readonly Color CrownColor = new Color(0.88f, 0.71f, 0.35f);

        public static readonly Texture2D Disc = MakeCircleTexture(distance => new Color(1f, 1f, 1f, InsideAlpha(distance)));
        public static readonly Texture2D Ring = MakeCircleTexture(distance => new Color(1f, 1f, 1f, InsideAlpha(distance) * OutsideAlpha(distance, 1f - RingThickness)));
        public static readonly Texture2D ThickRing = MakeCircleTexture(distance => new Color(1f, 1f, 1f, InsideAlpha(distance) * OutsideAlpha(distance, 1f - ThickRingThickness)));
        public static readonly Texture2D CornerMask = MakeCircleTexture(distance => new Color(TreeBackgroundColor.r, TreeBackgroundColor.g, TreeBackgroundColor.b, OutsideAlpha(distance, 1f)));
        public static readonly Texture2D Crown = ContentFinder<Texture2D>.Get("Icons/FirstKindredCrown", false) ?? MakeCrownTexture();

        private static float InsideAlpha(float distance) => Mathf.Clamp01((1f - distance) / PixelSize + 0.5f);

        private static float OutsideAlpha(float distance, float edge) => Mathf.Clamp01((distance - edge) / PixelSize + 0.5f);

        private static Texture2D MakeCircleTexture(Func<float, Color> colorAtDistance)
        {
            Color[] pixels = new Color[CircleTextureSize * CircleTextureSize];
            for (int pixelY = 0; pixelY < CircleTextureSize; pixelY++)
            {
                for (int pixelX = 0; pixelX < CircleTextureSize; pixelX++)
                {
                    float normalizedX = (pixelX + 0.5f) / CircleTextureSize * 2f - 1f;
                    float normalizedY = (pixelY + 0.5f) / CircleTextureSize * 2f - 1f;
                    pixels[pixelY * CircleTextureSize + pixelX] = colorAtDistance(Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY));
                }
            }
            return MakeTexture(CircleTextureSize, CircleTextureSize, pixels);
        }

        private static Texture2D MakeCrownTexture()
        {
            Vector2[][] spikes =
            {
                new[] { new Vector2(0.06f, 0.3f), new Vector2(0.4f, 0.3f), new Vector2(0.1f, 1f) },
                new[] { new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.3f), new Vector2(0.5f, 1f) },
                new[] { new Vector2(0.6f, 0.3f), new Vector2(0.94f, 0.3f), new Vector2(0.9f, 1f) }
            };

            Color[] pixels = new Color[CrownTextureWidth * CrownTextureHeight];
            for (int pixelY = 0; pixelY < CrownTextureHeight; pixelY++)
            {
                for (int pixelX = 0; pixelX < CrownTextureWidth; pixelX++)
                {
                    Vector2 point = new Vector2((pixelX + 0.5f) / CrownTextureWidth, (pixelY + 0.5f) / CrownTextureHeight);
                    bool insideBand = point.y <= 0.32f && point.x >= 0.06f && point.x <= 0.94f;
                    bool insideCrown = insideBand || spikes.Any(spike => IsInsideTriangle(point, spike[0], spike[1], spike[2]));
                    pixels[pixelY * CrownTextureWidth + pixelX] = insideCrown ? CrownColor : Color.clear;
                }
            }
            return MakeTexture(CrownTextureWidth, CrownTextureHeight, pixels);
        }

        private static bool IsInsideTriangle(Vector2 point, Vector2 firstCorner, Vector2 secondCorner, Vector2 thirdCorner)
        {
            float firstSide = SideOfEdge(point, firstCorner, secondCorner);
            float secondSide = SideOfEdge(point, secondCorner, thirdCorner);
            float thirdSide = SideOfEdge(point, thirdCorner, firstCorner);
            bool anyNegative = firstSide < 0f || secondSide < 0f || thirdSide < 0f;
            bool anyPositive = firstSide > 0f || secondSide > 0f || thirdSide > 0f;
            return !(anyNegative && anyPositive);
        }

        private static float SideOfEdge(Vector2 point, Vector2 edgeStart, Vector2 edgeEnd) => (point.x - edgeEnd.x) * (edgeStart.y - edgeEnd.y) - (edgeStart.x - edgeEnd.x) * (point.y - edgeEnd.y);

        private static Texture2D MakeTexture(int width, int height, Color[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
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