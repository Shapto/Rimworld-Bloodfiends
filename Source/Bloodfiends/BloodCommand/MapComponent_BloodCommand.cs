using Bloodfiends.Family;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
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
        public int maximumVisualsPerPass = 15;
        public float lineSeconds = 1.4f;        // how long one stream of blood takes to be pulled in
        public float lineWidth = 0.16f;         // how wide a stream is, in cells
        public float risingThickness = 2f;      // filth at least this thick (a pool) rises in streaks before it's pulled in

        public SimpleCurve radiusByGeneration;

        public List<ThingDef> extraBloodFilth = new List<ThingDef>();
    }

    public class MapComponent_BloodCommand : MapComponent
    {
        private static BloodCommandExtension cachedSettings;
        private readonly List<Pawn> bloodfiendsBuffer = new List<Pawn>();
        private static HashSet<ThingDef> cachedBloodFilthDefs;
        private readonly HashSet<Thing> filthBeingPulled = new HashSet<Thing>();   // claimed by a stream, gone once it's pulled in
        private int visualsThisPass;

        public static readonly BrushPalette BloodPalette = new BrushPalette(new Color(0.62f, 0.06f, 0.06f), new Color(0.45f, 0.02f, 0.03f), new Color(0.26f, 0f, 0.01f));
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
            filthBeingPulled.RemoveWhere(filth => filth.Destroyed);
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
                    if (!(bloodFilth[filthIndex] is Filth filth) || filth.Destroyed || filthBeingPulled.Contains(filth)) continue;
                    if ((filth.Position - bloodfiend.Position).LengthHorizontalSquared > radiusSquared) continue;

                    float hemogenGained = filth.thickness * Settings.hemogenPerFilthThickness;
                    GeneUtility.OffsetHemogen(bloodfiend, hemogenGained);
                    if (ShowAbsorb(filth.Position, bloodfiend, filth.thickness >= Settings.risingThickness, out StreamLineEffect stream))
                    {
                        filthBeingPulled.Add(filth);
                        stream.AtMoment(0.6f, () =>
                        {
                            if (!filth.Destroyed) filth.Destroy();
                        });
                    }
                    else
                    {
                        filth.Destroy();
                    }
                }
            }

            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (hemogenGene.Value >= hemogenGene.Max) return;
                if (!(thing is Corpse corpse) || (corpse.Position - bloodfiend.Position).LengthHorizontalSquared > radiusSquared) continue;

                float hemogenGained = DrainCorpse(corpse);
                if (hemogenGained <= 0f) continue;

                ShowAbsorb(corpse.Position, bloodfiend, true, out _);
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

        /// <summary>
        /// A stream of blood pulled from the source into the Bloodfiend. False (no stream) when there are already too many
        /// this pass, or the spot can't show effects.
        /// </summary>
        private bool ShowAbsorb(IntVec3 sourceCell, Pawn bloodfiend, bool risesFirst, out StreamLineEffect stream)
        {
            stream = null;
            if (visualsThisPass >= Settings.maximumVisualsPerPass || !sourceCell.ShouldSpawnMotesAt(map)) return false;
            visualsThisPass++;
            Vector3 source = sourceCell.ToVector3Shifted() + new Vector3(Rand.Range(-0.2f, 0.2f), 0f, Rand.Range(-0.2f, 0.2f));
            stream = new StreamLineEffect(BloodPalette, source, bloodfiend, Settings.lineWidth * Rand.Range(0.8f, 1.2f), risesFirst, Rand.Int, Settings.lineSeconds * Rand.Range(0.9f, 1.1f));
            MapComponent_BrushLines.Start(map, stream);
            return true;
        }
    }
}
