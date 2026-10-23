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
            RefreshCoagulate(node);
        }

        private void RefreshCoagulate(BloodfiendFamilyNode node)
        {
            if (pawn.abilities == null) return;
            AbilityDef coagulate = BloodfiendsDefOf.Bloodfiends_Coagulate;
            int maximumGeneration = coagulate.comps?.OfType<Bloodfiends.Coagulate.CompProperties_AbilityCoagulate>().FirstOrDefault()?.maximumCasterGeneration ?? 4;
            bool knowsIt = pawn.abilities.GetAbility(coagulate) != null;

            if (node.generationNumber <= maximumGeneration && !knowsIt) pawn.abilities.GainAbility(coagulate);
            else if (node.generationNumber > maximumGeneration && knowsIt) pawn.abilities.RemoveAbility(coagulate);
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
