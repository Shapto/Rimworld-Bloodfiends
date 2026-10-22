using SingularityFramework.Kitbash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Bloodfiends.Hardblood
{
    public class BloodbagGrowths
    {
        private const float Standoff = 1.1f;

        /// <summary>
        /// One infection site to roll: the angles round the body it may sit at, how far down (as a share of the surface),
        /// and how big its rock is (as a share of the surface's radius).
        /// </summary>
        private struct Site
        {
            public float angleMinimum, angleMaximum;
            public float downMinimum, downMaximum;
            public float reachMinimum, reachMaximum;
        }

        // the four sides of the body; sites are dealt out over them so none is left bare
        private static readonly float[][] Sides =
        {
            new[] { -45f, 45f },     // front
            new[] { 45f, 135f },     // the pawn's left
            new[] { 135f, 225f },    // back
            new[] { -135f, -45f }    // the pawn's right
        };

        public readonly SurfaceSpace head;
        public readonly SurfaceSpace torso;
        public readonly List<PlacedPiece> headCrystals;
        public readonly List<PlacedPiece> torsoCrystals;

        /// <summary>
        /// "southHead", "southBody": the textures seen from the front, at canvas size. "scale": canvas pixels per texture pixel.
        /// </summary>
        public BloodbagGrowths(PixelMask southHead, PixelMask southBody, List<KitbashPiece> pieces, int seed, int scale)
        {
            var random = new KitbashRandom(seed);
            head = new SurfaceSpace(southHead, -180f, 180f);
            torso = new SurfaceSpace(southBody, -180f, 180f);
            // rocks are sized against an average head and body, not this one, so a thin body gets rocks as big as anyone's
            headCrystals = Grow(head, pieces, random, HeadSites(random), 23 * scale, scale);
            torsoCrystals = Grow(torso, pieces, random, TorsoSites(random, random.RangeInclusive(4, 8)), 26 * scale, scale);
        }

        public KitbashCanvas HeadFacing(PixelMask viewHead, int rotation) => head.PlaceRigid(headCrystals, viewHead, rotation, Standoff);

        public KitbashCanvas BodyFacing(PixelMask viewBody, int rotation) => torso.PlaceRigid(torsoCrystals, viewBody, rotation, Standoff);

        /// <summary>
        /// Always one around an eye; sometimes more anywhere on the head, back included.
        /// </summary>
        private static List<Site> HeadSites(KitbashRandom random)
        {
            int eyeSide = random.Choice(new[] { -1, 1 });
            var sites = new List<Site>
            {
                new Site { angleMinimum = eyeSide * 15f, angleMaximum = eyeSide * 30f, downMinimum = 0.55f, downMaximum = 0.7f, reachMinimum = 0.75f, reachMaximum = 1f }
            };
            var extraSides = new List<float[]>(Sides);
            random.Shuffle(extraSides);
            int extraCount = random.WeightedChoice(new[] { 0, 1, 2 }, new[] { 0.4f, 0.4f, 0.2f });
            for (int index = 0; index < extraCount; index++)
            {
                sites.Add(new Site { angleMinimum = extraSides[index][0], angleMaximum = extraSides[index][1], downMinimum = 0.15f, downMaximum = 0.8f, reachMinimum = 0.45f, reachMaximum = 0.7f });
            }
            return sites;
        }

        /// <summary>
        /// The back always gets one; the rest are dealt round the other sides in random order, and over three bands of
        /// height the same way, so the rocks don't all bunch up at the shoulders.
        /// </summary>
        private static List<Site> TorsoSites(KitbashRandom random, int count)
        {
            var others = new List<float[]> { Sides[0], Sides[1], Sides[3] };
            random.Shuffle(others);
            var order = new List<float[]> { Sides[2] };
            order.AddRange(others);
            var bands = new List<float[]> { new[] { 0.05f, 0.3f }, new[] { 0.3f, 0.55f }, new[] { 0.55f, 0.8f } };
            random.Shuffle(bands);

            var sites = new List<Site>();
            for (int index = 0; index < count; index++)
            {
                float[] side = order[index % order.Count];
                float[] band = bands[index % bands.Count];
                sites.Add(new Site { angleMinimum = side[0], angleMaximum = side[1], downMinimum = band[0], downMaximum = band[1], reachMinimum = 0.45f, reachMaximum = 0.85f });
            }
            return sites;
        }

        /// <summary>
        /// One rock per site, sites kept apart so each stays its own rock. On a cramped body a site still goes in, wherever
        /// it's furthest from the others.
        /// </summary>
        private static List<PlacedPiece> Grow(SurfaceSpace space, List<KitbashPiece> pieces, KitbashRandom random, List<Site> sites, float minimumRadius, int scale)
        {
            // growths stay on the surface: anything standing off it would show as a crystal floating beside the body
            var surface = new PixelMask(space.width, space.height);
            for (int row = 0; row < space.height; row++)
            {
                if (space.radiusByRow[row] <= space.referenceRadius * 0.2f) continue;
                for (int column = 0; column < space.width; column++) surface.cells[row * space.width + column] = true;
            }
            PixelMask allowed = surface.Dilated(scale * 2);

            var crystals = new List<PlacedPiece>();
            var centers = new List<Vector2>();
            var reaches = new List<float>();
            foreach (Site site in sites)
            {
                float reach = Mathf.Max(space.referenceRadius, minimumRadius) * random.Uniform(site.reachMinimum, site.reachMaximum);
                Vector2 bestCenter = Vector2.zero;
                float bestSeparation = float.MinValue;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float angle = SurfaceSpace.WrapAngle(random.Uniform(site.angleMinimum, site.angleMaximum));
                    var center = new Vector2(space.ColumnForAngle(angle), space.RowForShare(random.Uniform(site.downMinimum, site.downMaximum)));
                    float separation = float.MaxValue;
                    for (int other = 0; other < centers.Count; other++) separation = Mathf.Min(separation, Vector2.Distance(center, centers[other]) / ((reach + reaches[other]) * 0.7f));
                    if (separation > bestSeparation)
                    {
                        bestSeparation = separation;
                        bestCenter = center;
                    }
                    if (separation > 1f) break;
                }
                centers.Add(bestCenter);
                reaches.Add(reach);
                float delay = 0.15f * ((centers.Count - 1) % 4) / 3f;
                crystals.AddRange(new Growth(space.width, space.height, allowed, pieces, random, bestCenter, reach, scale, delay).Build().layers);
            }
            return crystals;
        }

        /// <summary>
        /// One growth, spreading from its site: a big rock bursting out of it, sometimes a piece or two growing off that,
        /// sometimes a spike where it reaches furthest. Every piece points away from the site and grows out of the rock.
        /// </summary>
        private class Growth
        {
            public readonly List<PlacedPiece> layers = new List<PlacedPiece>();

            private readonly int width;
            private readonly int height;
            private readonly PixelMask allowed;
            private readonly List<KitbashPiece> pieces;
            private readonly KitbashRandom random;
            private readonly Vector2 center;
            private readonly float reach;
            private readonly int scale;
            private readonly PixelMask union;
            private readonly Dictionary<string, int> usage = new Dictionary<string, int>();
            private readonly float delay;   // when this site starts growing, as a share of the whole growth

            public Growth(int width, int height, PixelMask allowed, List<KitbashPiece> pieces, KitbashRandom random, Vector2 center, float reach, int scale, float delay)
            {
                this.width = width;
                this.height = height;
                this.allowed = allowed;
                this.pieces = pieces;
                this.random = random;
                this.center = center;
                this.reach = reach;
                this.scale = scale;
                this.delay = delay;
                union = new PixelMask(width, height);
            }

            public Growth Build()
            {
                List<KitbashPiece> large = pieces.FindAll(piece => piece.size == KitbashSize.Large);
                List<KitbashPiece> chunky = pieces.FindAll(piece => piece.size != KitbashSize.Small);
                List<KitbashPiece> pointed = pieces.FindAll(piece => piece.pointed);

                for (int attempt = 0; attempt < 30; attempt++)
                {
                    Vector2 jitter = new Vector2(random.Uniform(-0.1f, 0.1f), random.Uniform(-0.1f, 0.1f)) * reach;
                    if (GrowFrom(center + jitter, reach * random.Uniform(0.9f, 1.1f), random.Uniform(0.6f, 0.85f), large, 0.5f, 1f, 180f, 0f, 0.55f)) break;
                }
                if (layers.Count == 0) return this;

                int extraCount = random.WeightedChoice(new[] { 0, 1, 2 }, new[] { 0.6f, 0.3f, 0.1f });
                for (int attempt = 0; attempt < extraCount * 6 && layers.Count < 1 + extraCount; attempt++)
                {
                    List<Vector2> anchors = EdgeAnchors(reach * 0.6f);
                    if (anchors.Count == 0) break;
                    GrowFrom(random.Choice(anchors), reach * random.Uniform(0.55f, 0.75f), random.Uniform(0.55f, 0.8f), chunky, 0.35f, 0.35f, 40f, 0.45f, 0.35f);
                }

                int spikeCount = random.WeightedChoice(new[] { 0, 1 }, new[] { 0.6f, 0.4f });
                int spikesPlaced = 0;
                for (int attempt = 0; attempt < spikeCount * 8 && spikesPlaced < spikeCount; attempt++)
                {
                    List<Vector2> anchors = EdgeAnchors(reach * 1.2f);
                    if (anchors.Count == 0) break;
                    anchors.Sort((first, second) => Vector2.Distance(second, center).CompareTo(Vector2.Distance(first, center)));
                    Vector2 anchor = anchors[random.Index(Mathf.Max(1, anchors.Count / 3))];
                    if (GrowFrom(anchor, reach * random.Uniform(0.6f, 0.9f), random.Uniform(0.35f, 0.5f), pointed, 0.2f, 0.3f, 25f, 0.6f, 0.4f)) spikesPlaced++;
                }
                return this;
            }

            private bool GrowFrom(Vector2 anchor, float length, float widthShare, List<KitbashPiece> candidates, float embed, float maximumOverlap, float jitterDegrees, float growthStart, float growthSpan)
            {
                if (candidates.Count == 0) return false;
                Vector2 outward = anchor - center;
                float angle = outward.magnitude < 1e-6f ? random.Uniform(0f, 2f * Mathf.PI) : Mathf.Atan2(outward.y, outward.x);
                angle += random.Uniform(-jitterDegrees, jitterDegrees) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 across = new Vector2(-direction.y, direction.x);
                float halfWidth = length * widthShare / 2f;
                Vector2 baseMiddle = anchor - direction * length * embed;
                Vector2 tip = baseMiddle + direction * length;
                return Place(Pick(candidates), baseMiddle - across * halfWidth, baseMiddle + across * halfWidth, tip, maximumOverlap, growthStart, growthSpan);
            }

            private bool Place(KitbashPiece piece, Vector2 baseStart, Vector2 baseEnd, Vector2 tip, float maximumOverlap, float growthStart, float growthSpan)
            {
                bool mirrored = random.Chance(0.5f);
                Affine2D mirror = piece.MirrorTransform(mirrored);
                Vector2[] outline = piece.OutlinePoints(mirrored);
                List<Edge> bases = KitbashAssembly.BaseEdges(outline);
                if (bases.Count == 0) return false;
                Edge sourceBase = bases[0];
                foreach (Edge candidate in bases) if (candidate.Length > sourceBase.Length) sourceBase = candidate;
                Vector2 sourceTip = outline[0];
                foreach (Vector2 point in outline) if (Vector2.Distance(point, sourceBase.Middle) > Vector2.Distance(sourceTip, sourceBase.Middle)) sourceTip = point;

                foreach (Edge targetBase in new[] { new Edge(baseStart, baseEnd), new Edge(baseEnd, baseStart) })
                {
                    if (!Affine2D.TryFromThreePoints(sourceBase.start, sourceBase.end, sourceTip, targetBase.start, targetBase.end, tip, out Affine2D layBase)) continue;
                    Affine2D placement = Affine2D.Compose(layBase, mirror);
                    ConvexPolygon placedOutline = piece.outline.Transformed(placement);
                    if (placedOutline.Area < 1f) continue;
                    List<PixelSpan> spans = placedOutline.Rasterize(width, height);
                    int area = PixelMask.CountSpans(spans);
                    if (area == 0) continue;
                    if (layers.Count > 0)
                    {
                        float overlap = union.CountIn(spans) / (float)area;
                        if (overlap > maximumOverlap || overlap < 0.03f) continue;
                    }
                    if (allowed.CountIn(spans) / (float)area < 0.97f) continue;
                    if (NextToItself(piece, placedOutline)) continue;

                    layers.Add(new PlacedPiece
                    {
                        piece = piece,
                        placement = placement,
                        outline = placedOutline,
                        spans = spans,
                        inTiling = true,
                        growthOrigin = (baseStart + baseEnd) / 2f,
                        growthStart = delay + growthStart * (1f - delay),
                        growthSpan = growthSpan * (1f - delay)
                    });
                    union.Fill(spans);
                    usage[piece.name] = UsageOf(piece) + 1;
                    return true;
                }
                return false;
            }

            private bool NextToItself(KitbashPiece piece, ConvexPolygon outline)
            {
                for (int index = Mathf.Max(0, layers.Count - 3); index < layers.Count; index++)
                {
                    if (layers[index].piece == piece && outline.DistanceTo(layers[index].outline) < 2 * scale) return true;
                }
                return false;
            }

            private int UsageOf(KitbashPiece piece) => usage.TryGetValue(piece.name, out int count) ? count : 0;

            private KitbashPiece Pick(List<KitbashPiece> candidates)
            {
                var weights = new List<float>();
                foreach (KitbashPiece candidate in candidates) weights.Add(1f / (1 + UsageOf(candidate)));
                return random.WeightedChoice(candidates, weights);
            }

            private List<Vector2> EdgeAnchors(float maximumDistance)
            {
                var anchors = new List<Vector2>();
                List<List<Vector2>> rings = union.OutlineRings();
                if (rings.Count == 0) return anchors;
                List<Vector2> outline = rings[0];
                foreach (List<Vector2> ring in rings) if (ring.Count > outline.Count) outline = ring;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    Vector2 anchor = outline[random.Index(outline.Count)];
                    if (Vector2.Distance(anchor, center) <= maximumDistance) anchors.Add(anchor);
                }
                return anchors;
            }
        }
    }
}
