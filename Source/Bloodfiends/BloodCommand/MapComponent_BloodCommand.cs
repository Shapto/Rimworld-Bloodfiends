using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.BloodCommand
{
    public class BloodCommandExtension : DefModExtension
    {
        public int absorbIntervalTicks = 60;
        public float hemogenPerFilthThickness = 0.01f;
        public float hemogenPerCorpseBodySize = 0.3f;

        public SimpleCurve radiusByGeneration;

        public FleckDef absorbFleck;
    }

    public class MapComponent_BloodCommand : MapComponent
    {
        private static BloodCommandExtension cachedSettings;
        private readonly List<Pawn> bloodfiendsBuffer = new List<Pawn>();

        public MapComponent_BloodCommand(Map map) : base(map)
        {
        }

        private static BloodCommandExtension Settings
        {
            get
            {
                if (cachedSettings == null) cachedSettings = DefDatabase<GeneDef>.GetNamed("Bloodfiends_Bloodfiend").GetModExtension<BloodCommandExtension>() ?? new BloodCommandExtension();
                return cachedSettings;
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if ((Find.TickManager.TicksGame + map.uniqueID) % Settings.absorbIntervalTicks != 0) return;

            bloodfiendsBuffer.Clear();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (GenerationOf(pawn) > 0) bloodfiendsBuffer.Add(pawn);
            }
            if (bloodfiendsBuffer.Count == 0) return;

            bloodfiendsBuffer.SortBy(pawn => GenerationOf(pawn), pawn => pawn.thingIDNumber);
            foreach (Pawn bloodfiend in bloodfiendsBuffer)
                AbsorbAround(bloodfiend, GenerationOf(bloodfiend));

            bloodfiendsBuffer.Clear();
        }

        private static int GenerationOf(Pawn pawn)
        {
            if (pawn.Dead || pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active != true) return 0;
            return pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node?.generationNumber ?? 0;
        }

        private void AbsorbAround(Pawn bloodfiend, int generation)
        {
            Gene_Hemogen hemogenGene = bloodfiend.genes.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene == null) return;

            float radius = Settings.radiusByGeneration?.Evaluate(generation) ?? 10f;
            float radiusSquared = radius * radius;

            List<Thing> bloodFilth = map.listerThings.ThingsOfDef(ThingDefOf.Filth_Blood);
            for (int filthIndex = bloodFilth.Count - 1; filthIndex >= 0; filthIndex--)
            {
                if (hemogenGene.Value >= hemogenGene.Max) return;
                if (!(bloodFilth[filthIndex] is Filth filth) || filth.Destroyed) continue;
                if ((filth.Position - bloodfiend.Position).LengthHorizontalSquared > radiusSquared) continue;

                float hemogenGained = filth.thickness * Settings.hemogenPerFilthThickness;
                ShowAbsorb(filth.Position);
                filth.Destroy();
                GeneUtility.OffsetHemogen(bloodfiend, hemogenGained);
            }

            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (hemogenGene.Value >= hemogenGene.Max) return;
                if (!(thing is Corpse corpse) || (corpse.Position - bloodfiend.Position).LengthHorizontalSquared > radiusSquared) continue;

                float hemogenGained = DrainCorpse(corpse);
                if (hemogenGained <= 0f) continue;

                ShowAbsorb(corpse.Position);
                GeneUtility.OffsetHemogen(bloodfiend, hemogenGained);
            }
        }

        private static float DrainCorpse(Corpse corpse)
        {
            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn?.RaceProps.BloodDef != ThingDefOf.Filth_Blood) return 0f;

            Hediff bloodLoss = innerPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            float remainingBlood = 1f - (bloodLoss?.Severity ?? 0f);
            if (remainingBlood <= 0.01f) return 0f;

            if (bloodLoss == null)
            {
                bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, innerPawn);
                innerPawn.health.hediffSet.AddDirect(bloodLoss);
            }
            bloodLoss.Severity = 1f;

            return remainingBlood * innerPawn.BodySize * Settings.hemogenPerCorpseBodySize;
        }

        private void ShowAbsorb(IntVec3 sourceCell)
        {
            if (Settings.absorbFleck != null) FleckMaker.Static(sourceCell, map, Settings.absorbFleck);
        }
    }
}
