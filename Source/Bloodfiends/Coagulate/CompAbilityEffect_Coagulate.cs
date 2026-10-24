using Bloodfiends.BloodCommand;
using Bloodfiends.Hardblood;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Coagulate
{
    public class CompProperties_AbilityCoagulate : CompProperties_AbilityEffect
    {
        public HediffDef shellHediff;
        public SimpleCurve shellPointsByGeneration;              // how much damage the shell soaks, by the caster's tier
        public SimpleCurve regenerationMultiplierByGeneration;   // how much faster Sanguine Regeneration heals under it
        public FloatRange tendQualityRange = new FloatRange(0.5f, 0.8f);
        public float lineWidth = 0.14f;
        public float lineSeconds = 0.9f;
        public int maximumCasterGeneration = 6;   // only this Kindred tier and above can coagulate (First to Sixth)

        public CompProperties_AbilityCoagulate() => compClass = typeof(CompAbilityEffect_Coagulate);
    }

    public class CompAbilityEffect_Coagulate : CompAbilityEffect
    {
        public new CompProperties_AbilityCoagulate Props => (CompProperties_AbilityCoagulate)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;
            Pawn targetPawn = target.Pawn;
            if (targetPawn != null && !targetPawn.Dead && targetPawn.RaceProps.Humanlike && !targetPawn.HostileTo(parent.pawn)) return true;
            if (throwMessages) Messages.Message("Bloodfiends_CoagulateOnlyAllies".Translate(), targetPawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null) return;

            int generation = HardbloodWeaponry.GenerationOf(caster);
            float points = Props.shellPointsByGeneration?.Evaluate(generation) ?? 60f;
            float multiplier = Props.regenerationMultiplierByGeneration?.Evaluate(generation) ?? 1.5f;

            CloseBleedingWounds(targetPawn);
            if (targetPawn.health.hediffSet.GetFirstHediffOfDef(Props.shellHediff) is Hediff_HardbloodShell shell)
            {
                shell.Refill(points, multiplier);
            }
            else
            {
                var newShell = (Hediff_HardbloodShell)HediffMaker.MakeHediff(Props.shellHediff, targetPawn);
                newShell.shellPoints = points;
                newShell.maximumShellPoints = points;
                newShell.regenerationMultiplier = multiplier;
                targetPawn.health.AddHediff(newShell);
            }

            if (targetPawn != caster && caster.Spawned)
            {
                var stream = new StreamLineEffect(MapComponent_BloodCommand.BloodPalette, caster.DrawPos, targetPawn, Props.lineWidth, false, Rand.Int, Props.lineSeconds);
                MapComponent_BrushLines.Start(caster.Map, stream);
            }
        }

        private void CloseBleedingWounds(Pawn targetPawn)
        {
            foreach (Hediff hediff in targetPawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury && injury.Bleeding) injury.Tended(Props.tendQualityRange.RandomInRange, Props.tendQualityRange.TrueMax, 1);
            }
        }
    }
}
