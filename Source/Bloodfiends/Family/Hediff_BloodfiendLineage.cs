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
        public BloodfiendFamilyNode Node => GameComponent_BloodfiendFamilies.Instance?.GetNode(nodeId);
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            SyncFromNode();
        }

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

            float targetOffset = def.GetModExtension<LineageHemogenExtension>()?.hemogenMaxOffsetByGeneration?.Evaluate(node.generationNumber) ?? 0f;
            if (Mathf.Approximately(targetOffset, appliedHemogenMaxOffset)) return;

            hemogenGene.SetMax(hemogenGene.Max - appliedHemogenMaxOffset + targetOffset);
            appliedHemogenMaxOffset = targetOffset;
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            Gene_Hemogen hemogenGene = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene != null && appliedHemogenMaxOffset != 0f) hemogenGene.SetMax(hemogenGene.Max - appliedHemogenMaxOffset);
            if (pawn.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_Coagulate) != null) pawn.abilities.RemoveAbility(BloodfiendsDefOf.Bloodfiends_Coagulate);
            if (pawn.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_Mantle) != null) pawn.abilities.RemoveAbility(BloodfiendsDefOf.Bloodfiends_Mantle);
            if (pawn.abilities?.GetAbility(BloodfiendsDefOf.Bloodfiends_LaSangre) != null) pawn.abilities.RemoveAbility(BloodfiendsDefOf.Bloodfiends_LaSangre);
            appliedHemogenMaxOffset = 0f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nodeId, "nodeId", -1);
            Scribe_Values.Look(ref appliedHemogenMaxOffset, "appliedHemogenMaxOffset");
        }
    }
}
