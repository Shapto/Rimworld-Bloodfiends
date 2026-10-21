using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.BloodCommand
{
    public class BloodCommandExtension : DefModExtension
    {
        public int absorbIntervalTicks = 60;
        public float hemogenPerFilthThickness = 0.01f;
        public float hemogenPerCorpseBodySize = 0.3f;
        public int dropletsPerAbsorb = 3;
        public float dropletSpeed = 8f;
        public int maximumVisualsPerPass = 15;


        public SimpleCurve radiusByGeneration;

        public FleckDef absorbFleck;

        public List<ThingDef> extraBloodFilth = new List<ThingDef>();
    }

    public class MapComponent_BloodCommand : MapComponent
    {
        private static BloodCommandExtension cachedSettings;
        private readonly List<Pawn> bloodfiendsBuffer = new List<Pawn>();
        private static HashSet<ThingDef> cachedBloodFilthDefs;
        private int visualsThisPass;
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
            visualsThisPass = 0;
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

            foreach (ThingDef bloodFilthDef in BloodFilthDefs)
            {
                List<Thing> bloodFilth = map.listerThings.ThingsOfDef(bloodFilthDef);
                for (int filthIndex = bloodFilth.Count - 1; filthIndex >= 0; filthIndex--)
                {
                    if (hemogenGene.Value >= hemogenGene.Max) return;
                    if (!(bloodFilth[filthIndex] is Filth filth) || filth.Destroyed) continue;
                    if ((filth.Position - bloodfiend.Position).LengthHorizontalSquared > radiusSquared) continue;

                    float hemogenGained = filth.thickness * Settings.hemogenPerFilthThickness;
                    ShowAbsorb(filth.Position, bloodfiend);
                    filth.Destroy();
                    GeneUtility.OffsetHemogen(bloodfiend, hemogenGained);
                }
            }

            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (hemogenGene.Value >= hemogenGene.Max) return;
                if (!(thing is Corpse corpse) || (corpse.Position - bloodfiend.Position).LengthHorizontalSquared > radiusSquared) continue;

                float hemogenGained = DrainCorpse(corpse);
                if (hemogenGained <= 0f) continue;

                ShowAbsorb(corpse.Position, bloodfiend);
                GeneUtility.OffsetHemogen(bloodfiend, hemogenGained);
            }
        }

        private static HashSet<ThingDef> BloodFilthDefs
        {
            get
            {
                if (cachedBloodFilthDefs == null)
                {
                    cachedBloodFilthDefs = new HashSet<ThingDef> { ThingDefOf.Filth_Blood };
                    cachedBloodFilthDefs.AddRange(Settings.extraBloodFilth.Where(filthDef => filthDef != null));
                }
                return cachedBloodFilthDefs;
            }
        }

        private static float DrainCorpse(Corpse corpse)
        {
            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn == null || !BloodFilthDefs.Contains(innerPawn.RaceProps.BloodDef)) return 0f;
            if (innerPawn.RaceProps.FleshType == FleshTypeDefOf.Fleshbeast) return 0f;

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

        private void ShowAbsorb(IntVec3 sourceCell, Pawn bloodfiend)
        {
            FleckDef dropletFleck = Settings.absorbFleck;
            if (dropletFleck == null || visualsThisPass >= Settings.maximumVisualsPerPass || !sourceCell.ShouldSpawnMotesAt(map)) return;
            visualsThisPass++;

            Vector3 startPosition = sourceCell.ToVector3Shifted();
            Vector3 towardsBloodfiend = bloodfiend.DrawPos - startPosition;
            float distance = towardsBloodfiend.MagnitudeHorizontal();
            float angleToBloodfiend = towardsBloodfiend.AngleFlat();

            for (int dropletIndex = 0; dropletIndex < Settings.dropletsPerAbsorb; dropletIndex++)
            {
                float dropletSpeed = Settings.dropletSpeed * Rand.Range(0.85f, 1.15f);
                Vector3 jitteredStart = startPosition + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.3f, 0.3f));

                FleckCreationData dropletData = FleckMaker.GetDataStatic(jitteredStart, map, dropletFleck, Rand.Range(0.2f, 0.35f));
                dropletData.velocityAngle = angleToBloodfiend + Rand.Range(-6f, 6f);
                dropletData.velocitySpeed = dropletSpeed;
                dropletData.solidTimeOverride = distance / dropletSpeed;
                dropletData.rotation = dropletData.velocityAngle + 180f;
                map.flecks.CreateFleck(dropletData);
            }
        }
    }
}
