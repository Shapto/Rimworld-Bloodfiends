using RimWorld;
using UnityEngine;
using Verse;

namespace Bloodfiends.LaSangre
{
    public class CompProperties_AbilityLaSangre : CompProperties_AbilityEffect
    {
        public int maximumCasterGeneration = 1;            // the First Kindred's art
        public float minimumHemogenFraction = 0.5f;        // needs at least this much of a full bar to cast
        public float maximumStrength = 3f;                 // strength at a full bar spent (damage and stolen blood scale with it)
        public DamageDef damageDef;
        public float damage = 30f;                         // per enemy hit, before strength
        public float armorPenetration = 0.6f;
        public float impactRadius = 2.8f;
        public int stunTicks = 120;
        public float hemogenStolenPerEnemy = 0.08f;        // blood torn from each enemy hit, before strength, shared between the family
        public float familyRadius = 14f;                   // how far from the impact family members receive it
        public float airDistance = 7f;                     // how far back along the approach the First Kindred appears in the air
        public float airHeight = 3.5f;                     // how high (drawn north of where they are over the ground)
        public string lanceTexturePath = "Items/SanchoLanceAsc";
        public float lanceLength = 4.6f;                   // cells
        public float lanceGripFraction = 0.33f;            // where along the texture (left to right) the hands are
        public float lanceGuardFraction = 0.38f;           // the tendrils wrap from here to the tip
        public float cameraShake = 4f;
        public SoundDef impactSound;

        public CompProperties_AbilityLaSangre() => compClass = typeof(CompAbilityEffect_LaSangre);
    }

    public class CompAbilityEffect_LaSangre : CompAbilityEffect
    {
        public new CompProperties_AbilityLaSangre Props => (CompProperties_AbilityLaSangre)props;

        public override bool GizmoDisabled(out string reason)
        {
            Gene_Hemogen hemogenGene = parent.pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene == null || hemogenGene.Value < hemogenGene.Max * Props.minimumHemogenFraction)
            {
                reason = "Bloodfiends_LaSangreNeedsHemogen".Translate();
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Gene_Hemogen hemogenGene = caster.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene == null || !caster.Spawned) return;

            float spentFraction = hemogenGene.Max > 0f ? Mathf.Clamp01(hemogenGene.Value / hemogenGene.Max) : 0f;
            hemogenGene.Value = 0f;
            float strength = Mathf.Max(1f, Props.maximumStrength * spentFraction);
            LaSangreFlight.Launch(caster, target.Cell, strength, parent.def);
        }
    }
}