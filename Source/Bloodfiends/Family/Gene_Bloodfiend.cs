using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class Gene_Bloodfiend : Gene_Bloodfeeder
    {
        public Hediff_BloodfiendLineage LineageHediff => pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>();

        public override void PostAdd()
        {
            base.PostAdd();
            if (LineageHediff == null) RegisterAsNewBloodfiend();
        }

        /// <summary>
        /// Gives a Bloodfiend who wasn't embraced a place in the world: an offscreen ancestor of a player Bloodfiend
        /// if the claim roll succeeds, otherwise a new Family at a rolled tier.
        /// </summary>
        public void RegisterAsNewBloodfiend()
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null) return;

            BloodfiendStartingTierExtension startingTierSettings = def.GetModExtension<BloodfiendStartingTierExtension>();
            BloodfiendFamilyNode node = null;

            if (pawn.Faction != Faction.OfPlayer && startingTierSettings != null && Rand.Chance(startingTierSettings.joinPlayerFamilyChance)) node = registry.TryPlaceInPlayerRelatedFamily(pawn);
            if (node == null) node = registry.CreateFamilyFor(pawn, RollStartingGeneration(startingTierSettings));

            Hediff_BloodfiendLineage existingHediff = LineageHediff;
            if (existingHediff != null)
            {
                existingHediff.nodeId = node.nodeId;
                existingHediff.SyncFromNode();
                return;
            }

            Hediff_BloodfiendLineage lineageHediff = (Hediff_BloodfiendLineage)HediffMaker.MakeHediff(BloodfiendsDefOf.Bloodfiends_Lineage, pawn);
            lineageHediff.nodeId = node.nodeId;
            pawn.health.AddHediff(lineageHediff);
        }

        private static int RollStartingGeneration(BloodfiendStartingTierExtension startingTierSettings)
        {
            if (startingTierSettings == null || startingTierSettings.tierWeights.NullOrEmpty()) return 4;
            return startingTierSettings.tierWeights.RandomElementByWeight(tierWeight => tierWeight.weight).tier;
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode node = LineageHediff?.Node;
            if (registry == null || node == null) return;

            GameComponent_BloodfiendFamilies.SyncPromotedPawns(registry.Notify_BloodfiendDied(node));
        }
    }
}
