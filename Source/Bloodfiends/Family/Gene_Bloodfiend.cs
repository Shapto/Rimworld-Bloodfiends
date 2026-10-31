using Bloodfiends.Hardblood;
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
    public class Gene_Bloodfiend : Gene_Bloodfeeder
    {
        public Hediff_BloodfiendLineage LineageHediff => pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>();

        private const int FeastingUpdateIntervalTicks = 60;

        public HardbloodWeaponry weaponry = new HardbloodWeaponry();

        public override void PostAdd()
        {
            base.PostAdd();
            if (LineageHediff == null && GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(pawn) == null) RegisterAsNewBloodfiend();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(FeastingUpdateIntervalTicks, delta)) UpdateFeasting();
        }

        private void UpdateFeasting()
        {
            Gene_Hemogen hemogenGene = pawn.genes.GetFirstGeneOfType<Gene_Hemogen>();
            Hediff feastingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(BloodfiendsDefOf.Bloodfiends_Feasting);

            if (!Active || hemogenGene == null || hemogenGene.Max <= 0f)
            {
                if (feastingHediff != null) pawn.health.RemoveHediff(feastingHediff);
                return;
            }

            if (feastingHediff == null) feastingHediff = pawn.health.AddHediff(BloodfiendsDefOf.Bloodfiends_Feasting);
            feastingHediff.Severity = Mathf.Max(0.001f, hemogenGene.Value / hemogenGene.Max);
        }

        public override void PostRemove()
        {
            base.PostRemove();

            Hediff feastingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(BloodfiendsDefOf.Bloodfiends_Feasting);
            if (feastingHediff != null) pawn.health.RemoveHediff(feastingHediff);
        }

        public void RegisterAsNewBloodfiend()
        {
            GameComponent_BloodfiendFamilies registry = GameComponent_BloodfiendFamilies.Instance;
            if (registry == null) return;

            BloodfiendStartingTierExtension startingTierSettings = def.GetModExtension<BloodfiendStartingTierExtension>();
            BloodfiendFamilyNode node = null;

            if (GameComponent_BloodfiendFamilies.claimingNodeId >= 0) node = registry.TryClaimNode(pawn, GameComponent_BloodfiendFamilies.claimingNodeId);
            else if (GameComponent_BloodfiendFamilies.foundingGeneration > 0) node = registry.CreateFamilyFor(pawn, GameComponent_BloodfiendFamilies.foundingGeneration);
            else if (GameComponent_BloodfiendFamilies.placingLostKindred) node = registry.TryPlaceLostKindred(pawn);
            else if (GameComponent_BloodfiendFamilies.placingIntoFamily >= 0) node = registry.TryPlaceInFamily(pawn, GameComponent_BloodfiendFamilies.placingIntoFamily);
            else if (pawn.Faction != Faction.OfPlayer && startingTierSettings != null && Rand.Chance(startingTierSettings.joinPlayerFamilyChance)) node = registry.TryPlaceInPlayerRelatedFamily(pawn);
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

            Bloodfiends.SharedThirst.FamilialImpiety.Notify_ElderKilled(pawn, node, dinfo);
            GameComponent_BloodfiendFamilies.SyncPromotedPawns(registry.Notify_BloodfiendDied(node), pawn);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerable<Gizmo> baseGizmos = base.GetGizmos();
            if (baseGizmos != null) foreach (Gizmo gizmo in baseGizmos) yield return gizmo;
            if (Active && pawn.IsColonistPlayerControlled && pawn.Drafted) yield return weaponry.GetGizmo(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref weaponry, "hardbloodWeaponry");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && weaponry == null) weaponry = new HardbloodWeaponry();
        }
    }
}
