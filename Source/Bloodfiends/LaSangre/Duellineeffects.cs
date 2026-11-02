using System.Collections.Generic;
using SingularityFramework.Visuals.Brushlines;
using UnityEngine;
using Verse;

namespace Bloodfiends.LaSangre
{
    /// <summary>
    /// A shockwave: broken arcs of blood racing outward in a ring and thinning out as they go.
    /// </summary>
    public class RingLineEffect : BrushLineEffect
    {
        private readonly List<BrushStroke> strokes = new List<BrushStroke>();
        private readonly float startRadius;
        private readonly float endRadius;
        private float radius;

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public RingLineEffect(BrushPalette palette, Vector3 centre, float startRadius, float endRadius, float width, int arcCount, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            this.startRadius = startRadius;
            this.endRadius = endRadius;
            radius = startRadius;
            var random = new System.Random(seed);
            float arcSpan = Mathf.PI * 2f / arcCount;
            for (int index = 0; index < arcCount; index++)
            {
                float arcStart = index * arcSpan + (float)random.NextDouble() * arcSpan * 0.2f;
                float arcLength = arcSpan * (0.6f + (float)random.NextDouble() * 0.3f);
                strokes.Add(new BrushStroke(along => centre + new Vector3(Mathf.Cos(arcStart + along * arcLength), 0f, Mathf.Sin(arcStart + along * arcLength)) * radius, width, random.Next(), 5, 0.3f));
            }
        }

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            radius = Mathf.Lerp(startRadius, endRadius, 1f - Mathf.Pow(1f - progress, 3f));
            float alpha = 1f - BrushStroke.SmoothStep(0.4f, 1f, progress);
            foreach (BrushStroke stroke in strokes) stroke.Draw(drawer, 1f, 0f, alpha, BrushStroke.SmoothStep(0.3f, 0.9f, progress));
        }
    }

    /// <summary>
    /// Hardblood spikes bursting up out of the ground around a point, then crumbling away.
    /// </summary>
    public class SpikeLineEffect : BrushLineEffect
    {
        private readonly List<(BrushStroke stroke, float delay)> strokes = new List<(BrushStroke stroke, float delay)>();

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public SpikeLineEffect(BrushPalette palette, Vector3 centre, int count, FloatRange ringRadius, FloatRange length, FloatRange width, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            var random = new System.Random(seed);
            for (int index = 0; index < count; index++)
            {
                float angle = index / (float)count * Mathf.PI * 2f + Range(random, -0.2f, 0.2f);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 root = centre + outward * Range(random, ringRadius.min, ringRadius.max);
                Vector3 spike = (outward * 0.45f + Vector3.forward).normalized * Range(random, length.min, length.max);
                strokes.Add((new BrushStroke(along => root + spike * along, Range(random, width.min, width.max), random.Next(), 5, 0.15f), Range(random, 0f, 0.1f)));
            }
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            foreach (var entry in strokes)
            {
                float local = (progress - entry.delay) / (1f - entry.delay);
                if (local <= 0f || local >= 1f) continue;
                float head = BrushStroke.SmoothStep(0f, 0.12f, local);
                float tail = BrushStroke.SmoothStep(0.55f, 1f, local);
                entry.stroke.Draw(drawer, head, tail, 1f, BrushStroke.SmoothStep(0.5f, 0.9f, local));
            }
        }
    }

    /// <summary>
    /// A thick brush trail following a path given as points, drawn in as fast as the thing that left it, then wiped away.
    /// </summary>
    public class TrailLineEffect : BrushLineEffect
    {
        private readonly List<BrushStroke> strokes = new List<BrushStroke>();
        private readonly float drawInShare;

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public TrailLineEffect(BrushPalette palette, List<Vector3> points, float width, float drawInShare, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            this.drawInShare = drawInShare;
            List<Vector3> path = new List<Vector3>(points);
            strokes.Add(new BrushStroke(along => PointAlong(path, along), width, seed, 12, 0.3f));
            strokes.Add(new BrushStroke(along => PointAlong(path, along) + Vector3.forward * width * 0.7f, width * 0.15f, seed + 1, 3, 0.25f));
            strokes.Add(new BrushStroke(along => PointAlong(path, along) - Vector3.forward * width * 0.7f, width * 0.15f, seed + 2, 3, 0.25f));
        }

        private static Vector3 PointAlong(List<Vector3> path, float along)
        {
            if (path.Count == 1) return path[0];
            float scaled = Mathf.Clamp01(along) * (path.Count - 1);
            int index = Mathf.Min((int)scaled, path.Count - 2);
            return Vector3.Lerp(path[index], path[index + 1], scaled - index);
        }

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            float head = BrushStroke.SmoothStep(0f, drawInShare, progress);
            float tail = BrushStroke.SmoothStep(drawInShare + 0.15f, 1f, progress);
            float alpha = 1f - BrushStroke.SmoothStep(0.7f, 1f, progress);
            foreach (BrushStroke stroke in strokes) stroke.Draw(drawer, head, tail, alpha, BrushStroke.SmoothStep(0.5f, 1f, progress));
        }
    }

    /// <summary>
    /// A streak of blood flowing away along a direction with a gentle bend, sliding past like wind: its head races out
    /// and its tail chases after it, so it leaves nothing behind.
    /// </summary>
    public class FlowLineEffect : BrushLineEffect
    {
        private readonly BrushStroke stroke;

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public FlowLineEffect(BrushPalette palette, Vector3 start, Vector3 direction, float length, float bend, float width, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            Vector3 flow = new Vector3(direction.x, 0f, direction.z).normalized * length;
            Vector3 across = new Vector3(-flow.z, 0f, flow.x).normalized * bend;
            stroke = new BrushStroke(along => start + flow * along + across * along * along, width, seed, 6, 0.35f);
        }

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            float head = BrushStroke.SmoothStep(0f, 0.55f, progress);
            float tail = BrushStroke.SmoothStep(0.25f, 1f, progress);
            stroke.Draw(drawer, head, tail, 1f, BrushStroke.SmoothStep(0.6f, 1f, progress) * 0.6f);
        }
    }

    /// <summary>
    /// A broken lance: shards thrown out sideways from along its length, each arcing over and falling to the ground below.
    /// </summary>
    public class ShardLineEffect : BrushLineEffect
    {
        private readonly List<(BrushStroke stroke, float delay)> strokes = new List<(BrushStroke stroke, float delay)>();

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        public ShardLineEffect(BrushPalette palette, Vector3 from, Vector3 to, float fallHeight, int count, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            var random = new System.Random(seed);
            for (int index = 0; index < count; index++)
            {
                Vector3 start = Vector3.Lerp(from, to, (float)random.NextDouble());
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Vector3 thrown = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * 0.5f + 0.6f) * Range(random, 0.8f, 2f);
                float fall = fallHeight + thrown.z;
                strokes.Add((new BrushStroke(along => start + new Vector3(thrown.x * along, 0f, thrown.z * along - fall * along * along), Range(random, 0.08f, 0.16f), random.Next(), 3, 0.2f), Range(random, 0f, 0.15f)));
            }
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            foreach (var entry in strokes)
            {
                float local = (progress - entry.delay) / (1f - entry.delay);
                if (local <= 0f || local >= 1f) continue;
                float head = BrushStroke.SmoothStep(0f, 0.8f, local);
                float tail = Mathf.Max(0f, head - 0.18f);
                entry.stroke.Draw(drawer, head, tail, 1f - BrushStroke.SmoothStep(0.8f, 1f, local), 0.2f);
            }
        }
    }
}
