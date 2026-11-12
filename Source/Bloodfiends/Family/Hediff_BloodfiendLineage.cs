using Bloodfiends.LaSangre;
using Bloodfiends.Mantle;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Family
{
    public class LineageHemogenExtension : DefModExtension
    {
        public SimpleCurve hemogenMaxOffsetByGeneration;
    }

    public class Hediff_BloodfiendLineage : HediffWithComps
    {
        public int nodeId = -1;
        private float appliedHemogenMaxOffset;
        private Gene_Hemogen appliedHemogenGene;
        public BloodfiendFamilyNode Node => GameComponent_BloodfiendFamilies.Instance?.GetNode(nodeId);

        private bool curseLost;
        private bool superseded;
        public override bool ShouldRemove => base.ShouldRemove || curseLost || superseded;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (nodeId < 0) AdoptAddedTier();
            SyncFromNode();
        }


        private void AdoptAddedTier()
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null || !HasActiveDipsia) return;

            int chosenGeneration = Mathf.Max(GameComponent_BloodfiendFamilies.ElderGeneration, Mathf.RoundToInt(Severity));
            Hediff_BloodfiendLineage existingHediff = pawn.health.hediffSet.hediffs.OfType<Hediff_BloodfiendLineage>().FirstOrDefault(other => other != this && other.Node != null);
            if (existingHediff == null)
            {
                nodeId = registry.RefoundFamily(pawn, chosenGeneration).nodeId;
                return;
            }

            RetireAsDuplicate();
            if (existingHediff.Node.generationNumber == chosenGeneration) return;
            existingHediff.nodeId = registry.RefoundFamily(pawn, chosenGeneration).nodeId;
            existingHediff.SyncFromNode();
        }

        private void RepairMissingNode()
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null || superseded || !HasActiveDipsia) return;

            BloodfiendFamilyNode node = Node;
            if (node != null && node.bloodfiendPawn == pawn) return;
            if (node != null && node.bloodfiendPawn == null && registry.TryClaimNode(pawn, nodeId) != null) return;

            bool anotherLineageIsValid = pawn.health.hediffSet.hediffs.OfType<Hediff_BloodfiendLineage>().Any(other => other != this && other.Node?.bloodfiendPawn == pawn);
            if (anotherLineageIsValid)
            {
                RetireAsDuplicate();
                return;
            }

            BloodfiendFamilyNode ownNode = registry.GetNodeForPawn(pawn);
            nodeId = ownNode?.nodeId ?? registry.RefoundFamily(pawn, Mathf.Max(GameComponent_BloodfiendFamilies.ElderGeneration, Mathf.RoundToInt(Severity))).nodeId;
            SyncFromNode();
        }

        private void RetireAsDuplicate()
        {
            if (appliedHemogenGene != null && appliedHemogenMaxOffset != 0f) appliedHemogenGene.SetMax(appliedHemogenGene.Max - appliedHemogenMaxOffset);
            appliedHemogenMaxOffset = 0f;
            superseded = true;
        }

        private bool HasActiveDipsia => pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active == true && !Bloodfiends.Bloodbags.BloodbagUtility.IsBloodbag(pawn);

        public void SyncFromNode()
        {
            BloodfiendFamilyNode node = Node;
            if (node == null) return;

            Severity = node.generationNumber;
            RefreshHemogenMax(node);
            RefreshTierAbilities(node);
        }

        private void RefreshTierAbilities(BloodfiendFamilyNode node)
        {
            if (pawn.abilities == null) return;
            int coagulateGeneration = BloodfiendsDefOf.Bloodfiends_Coagulate.comps?.OfType<Bloodfiends.Coagulate.CompProperties_AbilityCoagulate>().FirstOrDefault()?.maximumCasterGeneration ?? 6;
            int mantleGeneration = BloodfiendsDefOf.Bloodfiends_Mantle.comps?.OfType<CompProperties_AbilityMantle>().FirstOrDefault()?.maximumCasterGeneration ?? 3;
            int laSangreGeneration = BloodfiendsDefOf.Bloodfiends_LaSangre.comps?.OfType<CompProperties_AbilityLaSangre>().FirstOrDefault()?.maximumCasterGeneration ?? 1;
            RefreshTierAbility(BloodfiendsDefOf.Bloodfiends_Coagulate, coagulateGeneration, node.generationNumber);
            RefreshTierAbility(BloodfiendsDefOf.Bloodfiends_Mantle, mantleGeneration, node.generationNumber);
            RefreshTierAbility(BloodfiendsDefOf.Bloodfiends_LaSangre, laSangreGeneration, node.generationNumber);
        }

        private void RefreshTierAbility(AbilityDef ability, int maximumGeneration, int generation)
        {
            bool knowsIt = pawn.abilities.GetAbility(ability) != null;
            if (generation <= maximumGeneration && !knowsIt) pawn.abilities.GainAbility(ability);
            else if (generation > maximumGeneration && knowsIt) pawn.abilities.RemoveAbility(ability);
        }

        private void RefreshHemogenMax(BloodfiendFamilyNode node)
        {
            Gene_Hemogen hemogenGene = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene == null) return;

            if (appliedHemogenGene != hemogenGene)
            {
                bool olderSaveAlreadyApplied = appliedHemogenGene == null && hemogenGene.Max >= 1f + appliedHemogenMaxOffset - 0.01f;
                if (!olderSaveAlreadyApplied) appliedHemogenMaxOffset = 0f;
                appliedHemogenGene = hemogenGene;
            }

            float targetOffset = def.GetModExtension<LineageHemogenExtension>()?.hemogenMaxOffsetByGeneration?.Evaluate(node.generationNumber) ?? 0f;
            if (Mathf.Approximately(targetOffset, appliedHemogenMaxOffset)) return;

            hemogenGene.SetMax(hemogenGene.Max - appliedHemogenMaxOffset + targetOffset);
            appliedHemogenMaxOffset = targetOffset;
        }
        public override void PostTick()
        {
            base.PostTick();
            if (!pawn.IsHashIntervalTick(250)) return;
            if (pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() == null)
            {
                curseLost = true;
                return;
            }
            RepairMissingNode();
            BloodfiendFamilyNode node = Node;
            if (node != null) RefreshHemogenMax(node);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (superseded) return;
            Gene_Hemogen hemogenGene = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene != null && hemogenGene == appliedHemogenGene && appliedHemogenMaxOffset != 0f) hemogenGene.SetMax(hemogenGene.Max - appliedHemogenMaxOffset);
            appliedHemogenGene = null;
            if (pawn.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_Coagulate) != null) pawn.abilities.RemoveAbility(BloodfiendsDefOf.Bloodfiends_Coagulate);
            if (pawn.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_Mantle) != null) pawn.abilities.RemoveAbility(BloodfiendsDefOf.Bloodfiends_Mantle);
            if (pawn.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_LaSangre) != null) pawn.abilities.RemoveAbility(BloodfiendsDefOf.Bloodfiends_LaSangre);
            appliedHemogenMaxOffset = 0f;
            if (curseLost) GameComponent_BloodfiendFamilies.Instance?.ReleaseClaim(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nodeId, "nodeId", -1);
            Scribe_Values.Look(ref appliedHemogenMaxOffset, "appliedHemogenMaxOffset");
            Scribe_References.Look(ref appliedHemogenGene, "appliedHemogenGene");
            Scribe_Values.Look(ref superseded, "superseded");
        }
    }
}
