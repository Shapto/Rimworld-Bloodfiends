using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class GameComponent_BloodfiendFamilies : GameComponent
    {
        public const int ElderGeneration = 1;

        public static int MaximumKindredsPerBloodfiend => BloodfiendsMod.Settings.maximumKindredsPerBloodfiend;

        private Dictionary<int, BloodfiendFamilyNode> nodesById = new Dictionary<int, BloodfiendFamilyNode>();
        private int nextNodeId = 1;
        private int nextFamilyId = 1;

        private List<int> nodeIdsWorkingList;
        private List<BloodfiendFamilyNode> nodesWorkingList;

        private Dictionary<Pawn, int> nodeIdsByPawn = new Dictionary<Pawn, int>();
        private readonly HashSet<Pawn> playerAncestorPawnsCache = new HashSet<Pawn>();
        private int playerAncestorCacheTick = -1;

        public static int embracingNodeId = -1;

        public BloodfiendFamilyNode GetNodeForPawn(Pawn pawn) => pawn != null && nodeIdsByPawn.TryGetValue(pawn, out int nodeId) ? GetNode(nodeId) : null;

        private static bool IsPlayerMember(BloodfiendFamilyNode node) => node.HasLivingPawn && node.bloodfiendPawn.Faction == Faction.OfPlayer;

        private static bool IsOutsideMember(BloodfiendFamilyNode node) => node.bloodfiendPawn == null || (!node.bloodfiendPawn.Dead && node.bloodfiendPawn.Faction != Faction.OfPlayer);

        private Dictionary<int, int> memberCountsByFamilyId = new Dictionary<int, int>();

        public int GetOtherFamilyMemberCount(int familyId) => memberCountsByFamilyId.TryGetValue(familyId, out int memberCount) ? Math.Max(0, memberCount - 1) : 0;

        public static GameComponent_BloodfiendFamilies Instance => Current.Game?.GetComponent<GameComponent_BloodfiendFamilies>();

        public IEnumerable<BloodfiendFamilyNode> NodesOfFamily(int familyId) => nodesById.Values.Where(node => node.familyId == familyId);

        private const int TutorialCheckIntervalTicks = 250;

        private bool tutorialLetterSent;

        /// <summary>
        /// Sends the Bloodfiend tutorial letter once per game, the first time one of the player's Bloodfiends is on a map.
        /// </summary>
        public override void GameComponentTick()
        {
            if (tutorialLetterSent || Find.TickManager.TicksGame % TutorialCheckIntervalTicks != 0) return;

            Pawn firstBloodfiend = nodesById.Values
                .Where(IsPlayerMember)
                .Select(node => node.bloodfiendPawn)
                .FirstOrDefault(pawn => pawn.Spawned);
            if (firstBloodfiend == null) return;

            tutorialLetterSent = true;
            Find.LetterStack.ReceiveLetter("Bloodfiends_TutorialLabel".Translate(), "Bloodfiends_TutorialText".Translate(firstBloodfiend.Named("PAWN")), LetterDefOf.NeutralEvent, firstBloodfiend);
        }

        /// <summary>
        /// RimWorld creates game components through reflection and needs exactly this constructor.
        /// </summary>
        public GameComponent_BloodfiendFamilies(Game game)
        {
        }

        public BloodfiendFamilyNode GetNode(int nodeId) => nodesById.TryGetValue(nodeId, out BloodfiendFamilyNode node) ? node : null;

        /// <summary>
        /// Starts a new Family for a Bloodfiend who has no embracer, with offscreen ancestors filling every generation above them.
        /// A First Kindred (Blood of the Primogenitor) gets no ancestors.
        /// </summary>
        public BloodfiendFamilyNode CreateFamilyFor(Pawn founderPawn, int startingGeneration)
        {
            int familyId = nextFamilyId++;
            int founderGeneration = Math.Max(ElderGeneration, startingGeneration);

            BloodfiendFamilyNode embracerNode = null;
            for (int ancestorGeneration = ElderGeneration; ancestorGeneration < founderGeneration; ancestorGeneration++)
                embracerNode = CreateNodeUnder(embracerNode, familyId, ancestorGeneration, null);

            return CreateNodeUnder(embracerNode, familyId, founderGeneration, founderPawn);
        }

        private void ChangeFamilyMemberCount(int familyId, int change)
        {
            memberCountsByFamilyId.TryGetValue(familyId, out int memberCount);
            memberCountsByFamilyId[familyId] = Math.Max(0, memberCount + change);
        }

        /// <summary>
        /// Creates a node under its embracer (or as a root when the embracer is null). Used by both new Families and Embrace.
        /// </summary>
        private BloodfiendFamilyNode CreateNodeUnder(BloodfiendFamilyNode embracerNode, int familyId, int generationNumber, Pawn bloodfiendPawn)
        {
            BloodfiendFamilyNode node = new BloodfiendFamilyNode
            {
                nodeId = nextNodeId++,
                familyId = familyId,
                generationNumber = generationNumber,
                bloodfiendPawn = bloodfiendPawn,
                embracerNodeId = embracerNode?.nodeId ?? -1,
                creatorNodeId = embracerNode?.nodeId ?? -1
            };
            RegisterNode(node);

            if (embracerNode != null)
            {
                embracerNode.embracedNodeIds.Add(node.nodeId);
                embracerNode.kindredCreatedCount++;
            }
            return node;
        }

        /// <summary>
        /// Places a newly spawned non-player Bloodfiend into a Family the player is part of, through one of that Family's
        /// outside members: either taking over an offscreen node, or as a new Kindred of an outside member with a free slot.
        /// Returns null when no player Family has anyone outside the colony.
        /// </summary>
        public BloodfiendFamilyNode TryPlaceInPlayerRelatedFamily(Pawn spawnedPawn)
        {
            HashSet<int> playerFamilyIds = new HashSet<int>(nodesById.Values
                .Where(IsPlayerMember)
                .Select(node => node.familyId));
            if (playerFamilyIds.Count == 0) return null;

            List<BloodfiendFamilyNode> outsideNodes = nodesById.Values
                .Where(node => playerFamilyIds.Contains(node.familyId))
                .Where(IsOutsideMember)
                .ToList();

            List<BloodfiendFamilyNode> claimableNodes = outsideNodes
                .Where(node => node.bloodfiendPawn == null)
                .ToList();
            List<BloodfiendFamilyNode> availableEmbracers = outsideNodes
                .Where(CanEmbraceKindred)
                .ToList();

            bool claimNode = claimableNodes.Count > 0 && (availableEmbracers.Count == 0 || Rand.Bool);
            if (claimNode)
            {
                BloodfiendFamilyNode claimedNode = claimableNodes.RandomElement();
                claimedNode.bloodfiendPawn = spawnedPawn;
                nodeIdsByPawn[spawnedPawn] = claimedNode.nodeId;
                playerAncestorCacheTick = -1;
                return claimedNode;
            }

            if (availableEmbracers.Count == 0) return null;
            BloodfiendFamilyNode embracerNode = availableEmbracers.RandomElement();
            return CreateNodeUnder(embracerNode, embracerNode.familyId, embracerNode.generationNumber + 1, spawnedPawn);
        }

        public int KindredSlotsUsedBy(BloodfiendFamilyNode embracerNode) => BloodfiendsMod.Settings.kindredSlotsFreeOnDeath ? nodesById.Values.Count(node => node.creatorNodeId == embracerNode.nodeId) : embracerNode.kindredCreatedCount;

        public bool CanEmbraceKindred(BloodfiendFamilyNode embracerNode) => embracerNode != null && KindredSlotsUsedBy(embracerNode) < MaximumKindredsPerBloodfiend;

        /// <summary>
        /// Adds a new Kindred one generation below its embracer, in the same Family.
        /// Returns null if the embracer has already used both Kindred slots.
        /// </summary>
        public BloodfiendFamilyNode CreateKindredNode(BloodfiendFamilyNode embracerNode, Pawn kindredPawn)
        {
            if (!CanEmbraceKindred(embracerNode)) return null;
            return CreateNodeUnder(embracerNode, embracerNode.familyId, embracerNode.generationNumber + 1, kindredPawn);
        }

        /// <summary>
        /// Promotes everyone below the dead Bloodfiend by one generation, moves their direct Kindreds
        /// under the dead one's embracer, and removes the dead node.
        /// Returns the promoted nodes so the caller can update their pawns.
        /// </summary>
        public List<BloodfiendFamilyNode> Notify_BloodfiendDied(BloodfiendFamilyNode deadNode)
        {
            if (deadNode == null || !nodesById.ContainsKey(deadNode.nodeId)) return new List<BloodfiendFamilyNode>();

            List<BloodfiendFamilyNode> promotedNodes = GetDescendants(deadNode).ToList();
            foreach (BloodfiendFamilyNode promotedNode in promotedNodes)
                promotedNode.generationNumber = Math.Max(ElderGeneration, promotedNode.generationNumber - 1);

            ReparentEmbracedKindreds(deadNode);
            nodesById.Remove(deadNode.nodeId);
            ChangeFamilyMemberCount(deadNode.familyId, -1);
            if (deadNode.bloodfiendPawn != null) nodeIdsByPawn.Remove(deadNode.bloodfiendPawn);
            playerAncestorCacheTick = -1;
            return promotedNodes;
        }

        public bool FamilyHasLivingBloodfiend(int familyId) => nodesById.Values.Any(node => node.familyId == familyId && node.HasLivingPawn);

        /// <summary>
        /// The dead Bloodfiend's direct Kindreds now answer to whoever embraced the dead one.
        /// If the dead one was a root, they become roots themselves, still in the same Family.
        /// </summary>
        private void ReparentEmbracedKindreds(BloodfiendFamilyNode deadNode)
        {
            BloodfiendFamilyNode grandparentNode = GetNode(deadNode.embracerNodeId);
            grandparentNode?.embracedNodeIds.Remove(deadNode.nodeId);

            foreach (int kindredNodeId in deadNode.embracedNodeIds)
            {
                BloodfiendFamilyNode kindredNode = GetNode(kindredNodeId);
                if (kindredNode == null) continue;

                kindredNode.embracerNodeId = grandparentNode?.nodeId ?? -1;
                grandparentNode?.embracedNodeIds.Add(kindredNodeId);
            }
        }

        /// <summary>
        /// Every node below the ancestor, walked breadth-first without recursion.
        /// The visited set protects against a corrupted save that loops back on itself.
        /// </summary>
        private IEnumerable<BloodfiendFamilyNode> GetDescendants(BloodfiendFamilyNode ancestorNode)
        {
            Queue<int> nodeIdsToVisit = new Queue<int>(ancestorNode.embracedNodeIds);
            HashSet<int> visitedNodeIds = new HashSet<int> { ancestorNode.nodeId };

            while (nodeIdsToVisit.Count > 0)
            {
                int nodeId = nodeIdsToVisit.Dequeue();
                if (!visitedNodeIds.Add(nodeId)) continue;

                BloodfiendFamilyNode descendantNode = GetNode(nodeId);
                if (descendantNode == null) continue;

                yield return descendantNode;

                foreach (int childNodeId in descendantNode.embracedNodeIds)
                    nodeIdsToVisit.Enqueue(childNodeId);
            }
        }

        /// <summary>
        /// Adds a node to both lookups. Every node creation goes through here.
        /// </summary>
        private void RegisterNode(BloodfiendFamilyNode node)
        {
            nodesById.Add(node.nodeId, node);
            if (node.bloodfiendPawn != null) nodeIdsByPawn[node.bloodfiendPawn] = node.nodeId;
            ChangeFamilyMemberCount(node.familyId, 1);
            playerAncestorCacheTick = -1;
        }

        /// <summary>
        /// True if a living Bloodfiend of the player's faction descends from this pawn,
        /// so this pawn's death would promote one of the player's own Kindreds.
        /// </summary>
        public bool IsAncestorOfPlayerBloodfiend(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;

            int currentTick = Find.TickManager.TicksGame;
            if (playerAncestorCacheTick != currentTick) RebuildPlayerAncestorCache(currentTick);
            return playerAncestorPawnsCache.Contains(pawn);
        }

        /// <summary>
        /// Walks up from every player Bloodfiend and collects their living ancestors.
        /// The shared visited set stops a walk as soon as it reaches a line that was already collected.
        /// </summary>
        private void RebuildPlayerAncestorCache(int currentTick)
        {
            playerAncestorPawnsCache.Clear();
            playerAncestorCacheTick = currentTick;
            HashSet<int> visitedNodeIds = new HashSet<int>();

            foreach (BloodfiendFamilyNode node in nodesById.Values)
            {
                if (!node.HasLivingPawn || node.bloodfiendPawn.Faction != Faction.OfPlayer) continue;

                BloodfiendFamilyNode ancestorNode = GetNode(node.embracerNodeId);
                while (ancestorNode != null && visitedNodeIds.Add(ancestorNode.nodeId))
                {
                    if (ancestorNode.HasLivingPawn) playerAncestorPawnsCache.Add(ancestorNode.bloodfiendPawn);
                    ancestorNode = GetNode(ancestorNode.embracerNodeId);
                }
            }
        }

        public static void SyncPromotedPawns(List<BloodfiendFamilyNode> promotedNodes, Pawn deadBloodfiend = null)
        {
            List<Pawn> promotedColonists = new List<Pawn>();
            StringBuilder builder = new StringBuilder();

            foreach (BloodfiendFamilyNode promotedNode in promotedNodes)
            {
                if (!promotedNode.HasLivingPawn) continue;
                Pawn promotedPawn = promotedNode.bloodfiendPawn;
                Hediff_BloodfiendLineage lineageHediff = promotedPawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>();
                if (lineageHediff == null) continue;

                string previousTierLabel = lineageHediff.CurStage?.label;
                List<AbilityDef> previousAbilities = promotedPawn.abilities?.abilities
                    .Select(ability => ability.def)
                    .ToList() ?? new List<AbilityDef>();

                lineageHediff.SyncFromNode();

                string newTierLabel = lineageHediff.CurStage?.label;
                if (promotedPawn.Faction != Faction.OfPlayer || previousTierLabel == newTierLabel) continue;

                promotedColonists.Add(promotedPawn);
                builder.AppendLine();
                builder.Append("Bloodfiends_PromotionLine".Translate(promotedPawn.Named("PAWN"), previousTierLabel.Named("OLD"), newTierLabel.Named("NEW")));

                List<string> learnedAbilityLabels = promotedPawn.abilities?.abilities
                    .Where(ability => !previousAbilities.Contains(ability.def))
                    .Select(ability => ability.def.label)
                    .ToList() ?? new List<string>();
                if (learnedAbilityLabels.Count > 0) builder.Append(" " + "Bloodfiends_PromotionLearned".Translate(learnedAbilityLabels.ToCommaList(useAnd: true)));
            }

            if (promotedColonists.Count == 0) return;

            string introduction = deadBloodfiend != null ? "Bloodfiends_PromotionTextDeath".Translate(deadBloodfiend.Named("ELDER")).Resolve() : "Bloodfiends_PromotionText".Translate().Resolve();
            Find.LetterStack.ReceiveLetter("Bloodfiends_PromotionLabel".Translate(), introduction + "\n" + builder, LetterDefOf.PositiveEvent, new LookTargets(promotedColonists));
        }

        public bool AreSameFamily(Pawn firstPawn, Pawn secondPawn)
        {
            BloodfiendFamilyNode firstNode = GetNodeForPawn(firstPawn);
            BloodfiendFamilyNode secondNode = GetNodeForPawn(secondPawn);
            return firstNode != null && secondNode != null && firstNode != secondNode && firstNode.familyId == secondNode.familyId;
        }

        public static bool placingLostKindred;

        public bool CanPlaceLostKindred() => LostKindredPlaces().Count > 0;

        public BloodfiendFamilyNode TryPlaceLostKindred(Pawn lostPawn)
        {
            List<(BloodfiendFamilyNode node, bool claimNode)> places = LostKindredPlaces();
            if (places.Count == 0) return null;
            (BloodfiendFamilyNode chosen, bool claimNode) = places.RandomElement();
            if (!claimNode) return CreateNodeUnder(chosen, chosen.familyId, chosen.generationNumber + 1, lostPawn);
            return TryClaimNode(lostPawn, chosen.nodeId);
        }

        private List<(BloodfiendFamilyNode node, bool claimNode)> LostKindredPlaces()
        {
            var bestPlayerGenerationByFamily = new Dictionary<int, int>();
            foreach (BloodfiendFamilyNode node in nodesById.Values.Where(IsPlayerMember))
            {
                bool known = bestPlayerGenerationByFamily.TryGetValue(node.familyId, out int bestGeneration);
                if (!known || node.generationNumber < bestGeneration) bestPlayerGenerationByFamily[node.familyId] = node.generationNumber;
            }

            var places = new List<(BloodfiendFamilyNode node, bool claimNode)>();
            foreach (BloodfiendFamilyNode node in nodesById.Values)
            {
                if (!IsOutsideMember(node) || !bestPlayerGenerationByFamily.TryGetValue(node.familyId, out int bestPlayerGeneration)) continue;
                if (node.bloodfiendPawn == null && node.generationNumber >= bestPlayerGeneration) places.Add((node, true));
                if (CanEmbraceKindred(node) && node.generationNumber + 1 >= bestPlayerGeneration) places.Add((node, false));
            }
            return places;
        }

        public Pawn PlayerRelativeIn(int familyId) => nodesById.Values
            .Where(node => node.familyId == familyId && IsPlayerMember(node))
            .OrderBy(node => node.generationNumber)
            .FirstOrDefault()?.bloodfiendPawn;

        public bool PlayerCanEmbraceKindred() => nodesById.Values.Any(node => IsPlayerMember(node) && CanEmbraceKindred(node));

        public static int placingIntoFamily = -1;

        public BloodfiendFamilyNode TryPlaceInFamily(Pawn pawn, int familyId)
        {
            List<BloodfiendFamilyNode> embracers = nodesById.Values
                .Where(node => node.familyId == familyId && CanEmbraceKindred(node))
                .ToList();
            if (embracers.Count == 0) return null;
            BloodfiendFamilyNode embracer = embracers.RandomElement();
            return CreateNodeUnder(embracer, familyId, embracer.generationNumber + 1, pawn);
        }

        public static int claimingNodeId = -1;

        public static int foundingGeneration = 0;

        public bool AnyPlayerBloodfiend() => nodesById.Values.Any(IsPlayerMember);

        public BloodfiendFamilyNode TryClaimNode(Pawn pawn, int nodeId)
        {
            BloodfiendFamilyNode node = GetNode(nodeId);
            if (node == null || node.bloodfiendPawn != null) return null;

            node.bloodfiendPawn = pawn;
            nodeIdsByPawn[pawn] = node.nodeId;
            playerAncestorCacheTick = -1;
            return node;
        }

        public void ReleaseClaim(Pawn pawn)
        {
            BloodfiendFamilyNode node = GetNodeForPawn(pawn);
            if (node == null) return;

            node.bloodfiendPawn = null;
            nodeIdsByPawn.Remove(pawn);
            playerAncestorCacheTick = -1;
        }

        public List<BloodfiendFamilyNode> UnclaimedAncestorsOfPlayer()
        {
            HashSet<int> visitedNodeIds = new HashSet<int>();
            List<BloodfiendFamilyNode> unclaimedAncestors = new List<BloodfiendFamilyNode>();

            foreach (BloodfiendFamilyNode playerNode in nodesById.Values.Where(IsPlayerMember))
            {
                BloodfiendFamilyNode ancestorNode = GetNode(playerNode.embracerNodeId);
                while (ancestorNode != null && visitedNodeIds.Add(ancestorNode.nodeId))
                {
                    if (ancestorNode.bloodfiendPawn == null) unclaimedAncestors.Add(ancestorNode);
                    ancestorNode = GetNode(ancestorNode.embracerNodeId);
                }
            }
            return unclaimedAncestors;
        }

        public Pawn PlayerDescendantOf(BloodfiendFamilyNode ancestorNode) => GetDescendants(ancestorNode)
            .Where(IsPlayerMember)
            .Select(node => node.bloodfiendPawn)
            .FirstOrDefault();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref nodesById, "nodesById", LookMode.Value, LookMode.Deep, ref nodeIdsWorkingList, ref nodesWorkingList);
            Scribe_Values.Look(ref nextNodeId, "nextNodeId", 1);
            Scribe_Values.Look(ref nextFamilyId, "nextFamilyId", 1);
            Scribe_Values.Look(ref tutorialLetterSent, "tutorialLetterSent");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (nodesById == null) nodesById = new Dictionary<int, BloodfiendFamilyNode>();

                memberCountsByFamilyId = new Dictionary<int, int>();
                foreach (BloodfiendFamilyNode node in nodesById.Values)
                {
                    ChangeFamilyMemberCount(node.familyId, 1);
                }

                nodeIdsByPawn = new Dictionary<Pawn, int>();
                foreach (BloodfiendFamilyNode node in nodesById.Values)
                {
                    if (node.bloodfiendPawn != null) nodeIdsByPawn[node.bloodfiendPawn] = node.nodeId;
                }
            }
        }
    }
}
