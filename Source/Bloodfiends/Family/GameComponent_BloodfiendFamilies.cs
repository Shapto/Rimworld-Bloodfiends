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

        public const int MaximumKindredsPerBloodfiend = 2;

        private Dictionary<int, BloodfiendFamilyNode> nodesById = new Dictionary<int, BloodfiendFamilyNode>();
        private int nextNodeId = 1;
        private int nextFamilyId = 1;

        private List<int> nodeIdsWorkingList;
        private List<BloodfiendFamilyNode> nodesWorkingList;

        public static GameComponent_BloodfiendFamilies Instance => Current.Game?.GetComponent<GameComponent_BloodfiendFamilies>();

        /// <summary>
        /// RimWorld creates game components through reflection and needs exactly this constructor.
        /// </summary>
        public GameComponent_BloodfiendFamilies(Game game)
        {
        }

        public BloodfiendFamilyNode GetNode(int nodeId) => nodesById.TryGetValue(nodeId, out BloodfiendFamilyNode node) ? node : null;

        /// <summary>
        /// Starts a brand new Family with this Bloodfiend at its root.
        /// </summary>
        public BloodfiendFamilyNode CreateRootNode(Pawn founderPawn, int startingGeneration)
        {
            BloodfiendFamilyNode rootNode = new BloodfiendFamilyNode
            {
                nodeId = nextNodeId++,
                familyId = nextFamilyId++,
                generationNumber = Math.Max(ElderGeneration, startingGeneration),
                bloodfiendPawn = founderPawn
            };

            nodesById.Add(rootNode.nodeId, rootNode);
            return rootNode;
        }

        public bool CanEmbraceKindred(BloodfiendFamilyNode embracerNode) => embracerNode != null && embracerNode.kindredCreatedCount < MaximumKindredsPerBloodfiend;

        /// <summary>
        /// Adds a new Kindred one generation below its embracer, in the same Family.
        /// Returns null if the embracer has already used both Kindred slots.
        /// </summary>
        public BloodfiendFamilyNode CreateKindredNode(BloodfiendFamilyNode embracerNode, Pawn kindredPawn)
        {
            if (!CanEmbraceKindred(embracerNode)) return null;

            BloodfiendFamilyNode kindredNode = new BloodfiendFamilyNode
            {
                nodeId = nextNodeId++,
                familyId = embracerNode.familyId,
                generationNumber = embracerNode.generationNumber + 1,
                bloodfiendPawn = kindredPawn,
                embracerNodeId = embracerNode.nodeId
            };

            nodesById.Add(kindredNode.nodeId, kindredNode);
            embracerNode.embracedNodeIds.Add(kindredNode.nodeId);
            embracerNode.kindredCreatedCount++;
            return kindredNode;
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

        public bool IsAncestorOfPlayerBloodfiend(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;

            BloodfiendFamilyNode pawnNode = nodesById.Values.FirstOrDefault(node => node.bloodfiendPawn == pawn);
            if (pawnNode == null) return false;

            return GetDescendants(pawnNode)
                .Any(descendantNode => descendantNode.HasLivingPawn && descendantNode.bloodfiendPawn.Faction == Faction.OfPlayer);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref nodesById, "nodesById", LookMode.Value, LookMode.Deep, ref nodeIdsWorkingList, ref nodesWorkingList);
            Scribe_Values.Look(ref nextNodeId, "nextNodeId", 1);
            Scribe_Values.Look(ref nextFamilyId, "nextFamilyId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && nodesById == null) nodesById = new Dictionary<int, BloodfiendFamilyNode>();
        }
    }
}
