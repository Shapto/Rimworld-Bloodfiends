using Bloodfiends.Bloodbags;
using Bloodfiends.SharedThirst;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class CompProperties_AbilityCoagulateAndMeltAway : CompProperties_AbilityEffect
    {
        public int maximumGenerationForBloodfiendTargets = 2;
        public int minimumTargetGeneration = 4;
        public float hemogenPerBodySize = 0.3f;

        public EffecterDef goreEffecter;

        public float goreRadius = 2.9f;
        public float goreFilthChancePerCell = 0.6f;

        public List<ThingDef> extraGoreFilth = new List<ThingDef>();

        public CompProperties_AbilityCoagulateAndMeltAway() => compClass = typeof(CompAbilityEffect_CoagulateAndMeltAway);
    }

    /// <summary>
    /// die mar die
    /// </summary>
    public class CompAbilityEffect_CoagulateAndMeltAway : CompAbilityEffect
    {
        public new CompProperties_AbilityCoagulateAndMeltAway Props => (CompProperties_AbilityCoagulateAndMeltAway)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            string rejectionReason = GetRejectionReason(target.Pawn);
            if (rejectionReason == null) return true;

            if (throwMessages) Messages.Message(rejectionReason, target.Pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        private string GetRejectionReason(Pawn targetPawn)
        {
            if (targetPawn == null || targetPawn.Dead) return "Bloodfiends_MustTargetLivingBloodfiend".Translate();
            if (IsBloodbag(targetPawn)) return null;

            BloodfiendFamilyNode targetNode = targetPawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node;
            if (targetNode == null) return "Bloodfiends_OnlyWorksOnBloodfiends".Translate();

            int casterGeneration = parent.pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node?.generationNumber ?? int.MaxValue;
            if (casterGeneration > Props.maximumGenerationForBloodfiendTargets) return "Bloodfiends_OnlyBloodbagsAtYourTier".Translate();
            if (targetNode.generationNumber < Props.minimumTargetGeneration) return "Bloodfiends_BloodTooStrong".Translate();
            return null;
        }

        private static bool IsBloodbag(Pawn pawn) => BloodbagUtility.IsBloodbag(pawn);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn.Dead) return;

            IntVec3 position = targetPawn.PositionHeld;
            Map map = targetPawn.MapHeld;

            FamilialImpiety.TryApply(parent.pawn, targetPawn);
            float targetHemogen = targetPawn.genes?.GetFirstGeneOfType<Gene_Hemogen>()?.Value ?? 0f;
            GeneUtility.OffsetHemogen(parent.pawn, targetHemogen + targetPawn.BodySize * Props.hemogenPerBodySize);
            targetPawn.Strip();
            targetPawn.Kill(new DamageInfo(DamageDefOf.Cut, 99999f, 999f, -1f, parent.pawn));
            targetPawn.Corpse?.Destroy();

            if (map != null) BurstIntoGore(position, map);
        }

        private void BurstIntoGore(IntVec3 position, Map map)
        {
            if (Props.goreEffecter != null)
            {
                Effecter goreEffecter = Props.goreEffecter.Spawn();
                goreEffecter.Trigger(new TargetInfo(position, map), new TargetInfo(position, map));
                goreEffecter.Cleanup();
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, Props.goreRadius, true))
            {
                if (!cell.InBounds(map) || !cell.Walkable(map) || !Rand.Chance(Props.goreFilthChancePerCell)) continue;

                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood);
                if (Props.extraGoreFilth.Count > 0 && Rand.Chance(0.3f)) FilthMaker.TryMakeFilth(cell, map, Props.extraGoreFilth.RandomElement());
            }
        }
    }
}
