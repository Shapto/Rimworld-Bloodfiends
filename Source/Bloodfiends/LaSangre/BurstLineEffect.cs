using SingularityFramework.Visuals.Brushlines;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Bloodfiends.LaSangre
{
    public class BurstLineEffect : BrushLineEffect
    {
        private readonly List<(BrushStroke stroke, float delay)> strokes = new List<(BrushStroke stroke, float delay)>();

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public BurstLineEffect(BrushPalette palette, Vector3 centre, int count, FloatRange reach, FloatRange width, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            var random = new System.Random(seed);
            for (int index = 0; index < count; index++)
            {
                float angle = index / (float)count * Mathf.PI * 2f + Range(random, -0.12f, 0.12f);
                float distance = Range(random, reach.min, reach.max);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                strokes.Add((new BrushStroke(along => centre + outward * along, Range(random, width.min, width.max), random.Next(), 6, 0.4f), Range(random, 0f, 0.08f)));
            }
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            foreach (var entry in strokes)
            {
                BrushStroke stroke = entry.stroke;
                float local = (progress - entry.delay) / (1f - entry.delay);
                if (local <= 0f || local >= 1f) continue;
                float head = BrushStroke.SmoothStep(0f, 0.35f, local);
                float tail = BrushStroke.SmoothStep(0.45f, 1f, local);
                stroke.Draw(drawer, head, tail, 1f, BrushStroke.SmoothStep(0.3f, 0.7f, local));
            }
        }
    }

    public class SmearLineEffect : BrushLineEffect
    {
        private readonly List<BrushStroke> strokes = new List<BrushStroke>();

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public SmearLineEffect(BrushPalette palette, Vector3 from, Vector3 to, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            Vector3 line = to - from;
            line.y = 0f;
            Vector3 across = new Vector3(-line.z, 0f, line.x).normalized;
            strokes.Add(new BrushStroke(along => from + line * along, 0.7f, seed, 12, 0.3f));
            strokes.Add(new BrushStroke(along => from + across * 0.55f + line * (0.1f + 0.9f * along), 0.08f, seed + 1, 3, 0.2f));
            strokes.Add(new BrushStroke(along => from - across * 0.55f + line * (0.1f + 0.9f * along), 0.08f, seed + 2, 3, 0.2f));
        }

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            float head = BrushStroke.SmoothStep(0f, 0.12f, progress);
            float tail = BrushStroke.SmoothStep(0.3f, 1f, progress);
            float alpha = 1f - BrushStroke.SmoothStep(0.6f, 1f, progress);
            foreach (BrushStroke stroke in strokes) stroke.Draw(drawer, head, tail, alpha, 0f);
        }
    }
}