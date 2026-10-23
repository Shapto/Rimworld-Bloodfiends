using Bloodfiends.Coagulate;
using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.SanguineRegeneration
{
    public class SanguineRegenerationExtension : DefModExtension
    {
        public int regenerationIntervalTicks = 60;
        public float hemogenReserveFraction = 0.1f;

        public SimpleCurve healingPerPassByGeneration;

        public float hemogenPerHealedPoint = 0.002f;
        public float regrowthCostMultiplier = 3f;
        public float bloodLossHealedPerPass = 0.02f;
        public float hemogenPerBloodLossSeverity = 0.2f;

        public float mortalStateDrainPerPass = 0.01f;

        public int defaultGeneration = 4;
    }

    public class Gene_SanguineRegeneration : Gene
    {
        private static readonly SanguineRegenerationExtension DefaultSettings = new SanguineRegenerationExtension();

        private int lastDeathPreventedTick = -99999;

        private SanguineRegenerationExtension Settings => def.GetModExtension<SanguineRegenerationExtension>() ?? DefaultSettings;

        private Gene_Hemogen HemogenGene => pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();

        private int Generation => pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node?.generationNumber ?? Settings.defaultGeneration;

        public bool InMortalState => Find.TickManager.TicksGame - lastDeathPreventedTick <= Settings.regenerationIntervalTicks * 2;

        /// <summary>
        /// Hemogen that can be spent without going below the reserve.
        /// </summary>
        private float AvailableHemogen
        {
            get
            {
                Gene_Hemogen hemogenGene = HemogenGene;
                return hemogenGene == null ? 0f : hemogenGene.Value - hemogenGene.Max * Settings.hemogenReserveFraction;
            }
        }

        public bool TryPreventDeath()
        {
            if (!Active || AvailableHemogen <= 0f || pawn.health.hediffSet.GetBrain() == null) return false;

            lastDeathPreventedTick = Find.TickManager.TicksGame;
            return true;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn.Dead || !pawn.IsHashIntervalTick(Settings.regenerationIntervalTicks, delta)) return;

            if (InMortalState)
            {
                GeneUtility.OffsetHemogen(pawn, -Mathf.Min(Settings.mortalStateDrainPerPass, Mathf.Max(0f, AvailableHemogen)));
                pawn.health.CheckForStateChange(null, null);
                if (pawn.Dead) return;
            }

            HealBloodLoss();

            float healingBudget = (Settings.healingPerPassByGeneration?.Evaluate(Generation) ?? 1f) * Hediff_HardbloodShell.RegenerationMultiplierFor(pawn);
            if (InMortalState)
            {
                TryRegrowOnePart();
                HealInjuries(healingBudget);
                return;
            }

            healingBudget = HealInjuries(healingBudget);
            if (healingBudget > 0f) TryRegrowOnePart();
        }

        private bool TrySpendHemogen(float hemogenCost)
        {
            if (hemogenCost > AvailableHemogen) return false;

            GeneUtility.OffsetHemogen(pawn, -hemogenCost);
            return true;
        }

        private void HealBloodLoss()
        {
            Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss == null) return;

            float severityHealed = Mathf.Min(bloodLoss.Severity, Settings.bloodLossHealedPerPass);
            if (TrySpendHemogen(severityHealed * Settings.hemogenPerBloodLossSeverity)) bloodLoss.Severity -= severityHealed;
        }

        /// <summary>
        /// Heals fresh injuries, bleeding ones first, then the worst. Scars are left to the Scarless gene.
        /// Returns whatever healing budget is left over.
        /// </summary>
        private float HealInjuries(float healingBudget)
        {
            List<Hediff_Injury> injuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(injury => !injury.IsPermanent())
                .OrderByDescending(injury => injury.Bleeding)
                .ThenByDescending(injury => injury.Severity)
                .ToList();

            foreach (Hediff_Injury injury in injuries)
            {
                if (healingBudget <= 0f) break;

                float healAmount = Mathf.Min(injury.Severity, healingBudget);
                if (!TrySpendHemogen(healAmount * Settings.hemogenPerHealedPoint)) break;

                injury.Heal(healAmount);
                healingBudget -= healAmount;
            }
            return healingBudget;
        }

        /// <summary>
        /// Regrows one missing part, vital parts first. Costs the part's max health times the regrowth multiplier.
        /// </summary>
        private void TryRegrowOnePart()
        {
            Hediff_MissingPart missingPart = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .OrderByDescending(part => part.Part.def.tags.Any(tag => tag.vital))
                .FirstOrDefault();
            if (missingPart == null) return;

            float regrowthCost = missingPart.Part.def.GetMaxHealth(pawn) * Settings.hemogenPerHealedPoint * Settings.regrowthCostMultiplier;
            if (TrySpendHemogen(regrowthCost)) pawn.health.RestorePart(missingPart.Part);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastDeathPreventedTick, "lastDeathPreventedTick", -99999);
        }
    }
}
