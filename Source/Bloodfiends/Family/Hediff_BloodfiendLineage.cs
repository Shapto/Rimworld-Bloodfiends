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
            RefreshTierAbilities(node);
            RefreshHemogenMax(node);
        }

        private void RefreshTierAbilities(BloodfiendFamilyNode node)
        {
            if (pawn.abilities == null) return;

            AbilityDef coagulateDef = BloodfiendsDefOf.Bloodfiends_CoagulateAndMeltAway;
            int maximumCasterGeneration = coagulateDef.comps.OfType<CompProperties_AbilityCoagulateAndMeltAway>().FirstOrDefault()?.maximumCasterGeneration ?? 2;

            bool hasAbility = pawn.abilities.GetAbility(coagulateDef) != null;
            bool shouldHaveAbility = node.generationNumber <= maximumCasterGeneration;

            if (shouldHaveAbility && !hasAbility) pawn.abilities.GainAbility(coagulateDef);
            else if (!shouldHaveAbility && hasAbility) pawn.abilities.RemoveAbility(coagulateDef);
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
