using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Mantle
{
    public class CompProperties_AbilityMantle : CompProperties_AbilityEffect
    {
        public HediffDef mantleHediff;
        public int maximumCasterGeneration = 3;   // only this Kindred tier and above can raise a Mantle

        public CompProperties_AbilityMantle() => compClass = typeof(CompAbilityEffect_Mantle);
    }

    public class CompAbilityEffect_Mantle : CompAbilityEffect
    {
        public new CompProperties_AbilityMantle Props => (CompProperties_AbilityMantle)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Hediff_Mantle mantle = caster.health.hediffSet.GetFirstHediffOfDef(Props.mantleHediff) as Hediff_Mantle;
            if (mantle == null)
            {
                mantle = (Hediff_Mantle)HediffMaker.MakeHediff(Props.mantleHediff, caster);
                caster.health.AddHediff(mantle);
            }
            mantle.Refresh();
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodForming, caster);
        }
    }
}
