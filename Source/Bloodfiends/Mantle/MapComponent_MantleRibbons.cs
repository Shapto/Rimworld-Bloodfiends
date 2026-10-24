using Bloodfiends.BloodCommand;
using SingularityFramework.Visuals.Brushlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Mantle
{
    public class MapComponent_MantleRibbons : MapComponent
    {
        private const float BehindPawn = 0.1f;
        private const float InFrontOfPawn = 0.1f;

        private readonly Dictionary<Hediff_Mantle, MantleRibbons> ribbonsByMantle = new Dictionary<Hediff_Mantle, MantleRibbons>();
        private readonly List<Hediff_Mantle> finishedMantles = new List<Hediff_Mantle>();
        private BrushLineDrawer backDrawer;
        private BrushLineDrawer frontDrawer;

        public MapComponent_MantleRibbons(Map map) : base(map)
        {
        }

        public static MapComponent_MantleRibbons For(Map map) => map?.GetComponent<MapComponent_MantleRibbons>();

        public MantleRibbons RibbonsOf(Hediff_Mantle mantle)
        {
            if (!ribbonsByMantle.TryGetValue(mantle, out MantleRibbons ribbons))
            {
                ribbons = new MantleRibbons(mantle.pawn.thingIDNumber);
                ribbonsByMantle[mantle] = ribbons;
            }
            return ribbons;
        }

        public override void MapComponentUpdate()
        {
            if (ribbonsByMantle.Count == 0) return;
            if (backDrawer == null)
            {
                float pawnAltitude = AltitudeLayer.Pawn.AltitudeFor();
                backDrawer = new BrushLineDrawer(MapComponent_BloodCommand.BloodPalette, pawnAltitude - BehindPawn);
                frontDrawer = new BrushLineDrawer(MapComponent_BloodCommand.BloodPalette, pawnAltitude + InFrontOfPawn);
            }
            float step = Find.TickManager.Paused ? 0f : Time.deltaTime * Find.TickManager.TickRateMultiplier;
            bool visible = Find.CurrentMap == map;

            foreach (KeyValuePair<Hediff_Mantle, MantleRibbons> entry in ribbonsByMantle)
            {
                Hediff_Mantle mantle = entry.Key;
                MantleRibbons ribbons = entry.Value;
                Pawn pawn = mantle.pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map != map)
                {
                    finishedMantles.Add(mantle);
                    continue;
                }
                if (mantle.removed) ribbons.FadeOut();
                ribbons.Advance(step, mantle.ChargeFraction);
                if (ribbons.Gone)
                {
                    finishedMantles.Add(mantle);
                    continue;
                }
                if (visible) ribbons.Draw(pawn.DrawPos, backDrawer, frontDrawer);
            }
            foreach (Hediff_Mantle mantle in finishedMantles) ribbonsByMantle.Remove(mantle);
            finishedMantles.Clear();

            if (!visible) return;
            backDrawer.DrawAll();
            frontDrawer.DrawAll();
        }
    }

    public class MantleRibbons
    {
        private const int RibbonCount = 5;
        private const int Samples = 28;
        private const int CurlSamples = 12;
        private const float WallRadius = 0.56f;
        private const float WallHalfSpan = 0.85f;      // radians either side of the wall's facing
        private const float WallTopHeight = 0.56f;
        private const float WallRowSpacing = 0.15f;
        private const float WeaveAmplitude = 0.055f;
        private const float CurlRadius = 0.11f;
        private const float LooseWhileRetangling = 0.15f;
        private const float RetangledWithin = 15f;     // degrees off the new threat at which the ribbons tangle again
        private const float FadeInSeconds = 0.4f;
        private const float FadeOutSeconds = 0.6f;

        private class Ribbon
        {
            public float phase;
            public float span;
            public float radius;
            public float height;
            public float squash;
            public float halfWidth;
            public float speed;
            public bool curls;
            public float wallHeight;
            public float weavePhase;
            public float spin;
            public float swirlOffset;        // radians from its wall spot to its swirl spot, tracked without wrapping
            public float lastSwirlStart;
            public float lastWallStart;
            public bool offsetKnown;
        }

        private readonly List<Ribbon> ribbons = new List<Ribbon>();
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> halfWidths = new List<float>();
        private readonly List<bool> inFront = new List<bool>();

        private float facing;              // degrees, 0 = east, counterclockwise
        private float targetFacing;
        private float facingVelocity;
        private float tightness;           // 0 swirling, 1 a tight wall
        private float tightnessVelocity;
        private float shownCharge = -1f;
        private float shownChargeVelocity;
        private float holdSeconds;
        private bool retangling;
        private float surge;               // swell and brighten right after a block
        private float fade;
        private bool fadingOut;
        private float ageSeconds;

        public bool Gone => fadingOut && fade <= 0f;

        public MantleRibbons(int seed)
        {
            var random = new System.Random(seed);
            for (int index = 0; index < RibbonCount; index++)
            {
                ribbons.Add(new Ribbon
                {
                    phase = Range(random, 0f, Mathf.PI * 2f),
                    span = Range(random, 2.2f, 3.4f),
                    radius = Range(random, 0.55f, 0.72f),
                    height = Range(random, -0.09f, 0.23f),
                    squash = Range(random, 0.3f, 0.42f),
                    halfWidth = Range(random, 0.07f, 0.11f),
                    speed = Range(random, 0.8f, 1.2f) * (random.NextDouble() < 0.3 ? -1f : 1f),
                    curls = random.NextDouble() < 0.6,
                    wallHeight = WallTopHeight - index * WallRowSpacing,
                    weavePhase = Range(random, 0f, Mathf.PI * 2f)
                });
            }
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        public void Threat(float angle, MantleExtension settings)
        {
            holdSeconds = settings.wallHoldSeconds;
            if (tightness < 0.05f && !retangling)
            {
                facing = angle;
                facingVelocity = 0f;
            }
            else if (tightness >= 0.5f && Mathf.Abs(Mathf.DeltaAngle(facing, angle)) >= settings.minimumWallAngle)
            {
                retangling = true;
            }
            targetFacing = angle;
        }

        public void Blocked(float angle, MantleExtension settings)
        {
            Threat(angle, settings);
            surge = 1f;
        }

        public void FadeOut() => fadingOut = true;

        public void Advance(float step, float chargeFraction)
        {
            if (shownCharge < 0f) shownCharge = chargeFraction;
            if (step <= 0f) return;
            ageSeconds += step;
            holdSeconds -= step;

            if (retangling && Mathf.Abs(Mathf.DeltaAngle(facing, targetFacing)) < RetangledWithin) retangling = false;
            float wantedTightness = holdSeconds <= 0f ? 0f : retangling ? LooseWhileRetangling : 1f;
            tightness = Mathf.SmoothDamp(tightness, wantedTightness, ref tightnessVelocity, retangling ? 0.05f : 0.09f, Mathf.Infinity, step);
            facing = Mathf.SmoothDampAngle(facing, targetFacing, ref facingVelocity, retangling ? 0.1f : 0.07f, Mathf.Infinity, step);
            shownCharge = Mathf.SmoothDamp(shownCharge, chargeFraction, ref shownChargeVelocity, 0.4f, Mathf.Infinity, step);
            surge = Mathf.MoveTowards(surge, 0f, step * 2.5f);
            fade = fadingOut ? Mathf.MoveTowards(fade, 0f, step / FadeOutSeconds) : Mathf.MoveTowards(fade, 1f, step / FadeInSeconds);

            float looseness = 1f - tightness;
            float untangleSpin = retangling ? 3f * looseness : 0f;
            foreach (Ribbon ribbon in ribbons)
            {
                ribbon.spin += step * ribbon.speed * (1.1f * looseness + untangleSpin);
                TrackSwirlOffset(ribbon, looseness);
            }
        }

        private float WallStartOf(Ribbon ribbon) => facing * Mathf.Deg2Rad - WallHalfSpan + Mathf.Sin(ribbon.weavePhase) * 0.15f;

        private void TrackSwirlOffset(Ribbon ribbon, float looseness)
        {
            float wallStart = WallStartOf(ribbon);
            float swirlStart = ribbon.phase + ribbon.spin;
            if (!ribbon.offsetKnown || looseness > 0.99f)
            {
                ribbon.swirlOffset = Mathf.DeltaAngle(wallStart * Mathf.Rad2Deg, swirlStart * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                ribbon.offsetKnown = true;
            }
            else
            {
                float swirlMoved = Mathf.DeltaAngle(ribbon.lastSwirlStart * Mathf.Rad2Deg, swirlStart * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float wallMoved = Mathf.DeltaAngle(ribbon.lastWallStart * Mathf.Rad2Deg, wallStart * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                ribbon.swirlOffset += swirlMoved - wallMoved;
            }
            ribbon.lastSwirlStart = swirlStart;
            ribbon.lastWallStart = wallStart;
        }

        public void Draw(Vector3 center, BrushLineDrawer backDrawer, BrushLineDrawer frontDrawer)
        {
            if (fade <= 0f) return;
            for (int index = 0; index < ribbons.Count; index++)
            {
                float presence = Mathf.Clamp01(shownCharge * RibbonCount - index);
                if (presence <= 0f) continue;
                BuildRibbon(ribbons[index], center, presence);
                float strength = fade * (0.35f + 0.65f * presence);
                // a faint wide glow, then the dark body, the main red and the bright core on top
                DrawStrip(backDrawer, frontDrawer, 2, 1.7f, 0.15f * strength);
                DrawStrip(backDrawer, frontDrawer, 2, 1f, (0.45f + 0.25f * tightness + 0.2f * surge) * strength);
                DrawStrip(backDrawer, frontDrawer, 1, 0.65f, (0.55f + 0.2f * tightness + 0.2f * surge) * strength);
                DrawStrip(backDrawer, frontDrawer, 0, 0.22f, (0.7f + 0.2f * tightness + 0.1f * surge) * strength);
            }
        }

        private void BuildRibbon(Ribbon ribbon, Vector3 center, float presence)
        {
            points.Clear();
            halfWidths.Clear();
            inFront.Clear();
            float looseness = 1f - tightness;
            if (!ribbon.offsetKnown) TrackSwirlOffset(ribbon, 1f);
            float start = WallStartOf(ribbon) + ribbon.swirlOffset * looseness;
            float span = ribbon.span * looseness + 2f * WallHalfSpan * tightness;
            float radius = (ribbon.radius * looseness + WallRadius * tightness) * (1f + 0.15f * surge);
            float squash = ribbon.squash * (1f - 0.5f * tightness);
            float widthScale = (1f + 0.5f * tightness) * (0.55f + 0.45f * presence) * (1f + 0.4f * surge);

            for (int sample = 0; sample <= Samples; sample++)
            {
                float along = (float)sample / Samples;
                float angle = start + along * span;
                float weave = Mathf.Sin(along * 9f + ribbon.weavePhase + ageSeconds * 4f) * WeaveAmplitude * tightness;
                float height = ribbon.height * looseness + (ribbon.wallHeight + weave) * tightness;
                points.Add(center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius * squash + height));
                halfWidths.Add(ribbon.halfWidth * widthScale * Mathf.Pow(Mathf.Sin(Mathf.PI * along), 0.6f));
                inFront.Add(Mathf.Sin(angle) < 0f);
            }
            if (!ribbon.curls) return;

            Vector3 end = points[points.Count - 1];
            Vector3 heading = end - points[points.Count - 3];
            float headingAngle = Mathf.Atan2(heading.z, heading.x);
            bool endInFront = inFront[inFront.Count - 1];
            Vector3 curlCenter = end + new Vector3(Mathf.Cos(headingAngle + Mathf.PI / 2f), 0f, Mathf.Sin(headingAngle + Mathf.PI / 2f) * 0.8f) * CurlRadius;
            for (int sample = 1; sample <= CurlSamples; sample++)
            {
                float share = (float)sample / CurlSamples;
                float shrink = 1f - share * 0.8f;
                float angle = headingAngle - Mathf.PI / 2f + share * 5f;
                points.Add(curlCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * 0.8f) * CurlRadius * shrink);
                halfWidths.Add(ribbon.halfWidth * 0.55f * shrink * widthScale);
                inFront.Add(endInFront);
            }
        }

        private void DrawStrip(BrushLineDrawer backDrawer, BrushLineDrawer frontDrawer, int band, float widthFactor, float alpha)
        {
            if (alpha <= 0f) return;
            Vector3 previousLeft = Vector3.zero, previousRight = Vector3.zero;
            for (int index = 0; index < points.Count; index++)
            {
                Vector3 ahead = points[Mathf.Min(index + 1, points.Count - 1)] - points[Mathf.Max(index - 1, 0)];
                ahead.y = 0f;
                Vector3 direction = ahead.sqrMagnitude > 1e-10f ? ahead.normalized : Vector3.right;
                Vector3 normal = new Vector3(-direction.z, 0f, direction.x) * (halfWidths[index] * widthFactor);
                Vector3 left = points[index] + normal;
                Vector3 right = points[index] - normal;
                if (index > 0) (inFront[index - 1] ? frontDrawer : backDrawer).AddQuad(band, Mathf.Clamp01(alpha), previousLeft, left, right, previousRight);
                previousLeft = left;
                previousRight = right;
            }
        }
    }
}
