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
    public class Hediff_BloodfiendLineage : HediffWithComps
    {
        public int nodeId = -1;
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nodeId, "nodeId", -1);
        }
    }
}
