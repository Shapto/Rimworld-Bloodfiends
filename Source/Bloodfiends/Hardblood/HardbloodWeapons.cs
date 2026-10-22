using SingularityFramework.Kitbash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Bloodfiends.Hardblood
{
    public static class HardbloodWeapons
    {
        public static KitbashCanvas Coating(Color32[] weaponPixels, int width, int height, List<KitbashPiece> pieces, int seed, int scale)
        {
            var random = new KitbashRandom(seed);
            PixelMask visible = WeaponProfile.Visible(weaponPixels, width, height);
            var profile = new WeaponProfile(visible);
            var space = new WeaponSpace(profile, visible, 14 * scale, 2f);
            PixelMask target = space.silhouette.Dilated(scale);
            PixelMask allowed = target.Dilated(scale);
            var assembly = new KitbashAssembly(target, allowed, pieces, random, BigLength(space, scale), scale) { crowned = false };
            assembly.Build();

            var full = new List<PlacedPiece>(assembly.placed);
            assembly.placed.Clear();
            foreach (PlacedPiece placedPiece in full) if (random.Value() > 0.35f) assembly.placed.Add(placedPiece);
            KeepPatchinessEven(assembly, full, space.silhouette);

            KitbashCanvas canvas = space.BackToImage(assembly.Render());
            AddSweepingShards(canvas, profile, pieces, random);
            canvas.Harden();
            return canvas;
        }

        public static KitbashCanvas Replica(Color32[] weaponPixels, int width, int height, List<KitbashPiece> pieces, int seed, int scale, bool pointed)
        {
            var random = new KitbashRandom(seed);
            var profile = new WeaponProfile(WeaponProfile.Visible(weaponPixels, width, height));
            PixelMask body = WeaponProfile.Body(weaponPixels, width, height, scale);
            var space = new WeaponSpace(profile, body, 14 * scale, 1f);

            PixelMask target = Thickened(Swayed(space.silhouette, random, scale), profile);
            if (pointed) target = space.PointedEnd(target, 0.2f);
            PixelMask allowed = target.Dilated(scale);

            KitbashCanvas best = null;
            float bestCoverage = -1f;
            int targetArea = Mathf.Max(1, target.Count());
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var assembly = new KitbashAssembly(target, allowed, pieces, random, BigLength(space, scale), scale) { crowned = false };
                assembly.Build();
                KitbashCanvas rendered = assembly.Render();
                float coverage = rendered.SolidMask().And(target).Count() / (float)targetArea;
                if (coverage > bestCoverage)
                {
                    bestCoverage = coverage;
                    best = rendered;
                }
                if (coverage > 0.97f) break;
            }
            best.FillBare(target);
            best.CutTo(target);
            KitbashCanvas canvas = space.BackToImage(best);
            canvas.Harden();
            return canvas;
        }

        private static float BigLength(WeaponSpace space, int scale)
        {
            var thicknesses = new List<float>();
            for (int column = 0; column < space.width; column++)
            {
                int count = 0;
                for (int row = 0; row < space.height; row++) if (space.silhouette.cells[row * space.width + column]) count++;
                if (count > 0) thicknesses.Add(count);
            }
            if (thicknesses.Count == 0) return 10 * scale;
            thicknesses.Sort();
            return Mathf.Max(thicknesses[thicknesses.Count / 2] * 1.1f, 10 * scale);
        }

        private static void KeepPatchinessEven(KitbashAssembly assembly, List<PlacedPiece> full, PixelMask weapon)
        {
            var patchRandom = new KitbashRandom(full.Count);
            if (Coverage(assembly.placed, weapon) < 0.45f)
            {
                var missing = full.FindAll(placedPiece => !assembly.placed.Contains(placedPiece));
                patchRandom.Shuffle(missing);
                foreach (PlacedPiece placedPiece in missing)
                {
                    assembly.placed.Add(placedPiece);
                    if (Coverage(assembly.placed, weapon) >= 0.55f) break;
                }
            }
            else if (Coverage(assembly.placed, weapon) > 0.8f)
            {
                var kept = new List<PlacedPiece>(assembly.placed);
                patchRandom.Shuffle(kept);
                foreach (PlacedPiece placedPiece in kept)
                {
                    assembly.placed.Remove(placedPiece);
                    if (Coverage(assembly.placed, weapon) <= 0.7f) break;
                }
            }
        }

        private static float Coverage(List<PlacedPiece> placed, PixelMask weapon)
        {
            var covered = new PixelMask(weapon.width, weapon.height);
            foreach (PlacedPiece placedPiece in placed) covered.Fill(placedPiece.spans);
            return covered.And(weapon).Count() / (float)Mathf.Max(1, weapon.Count());
        }

        private static void AddSweepingShards(KitbashCanvas canvas, WeaponProfile profile, List<KitbashPiece> pieces, KitbashRandom random)
        {
            List<KitbashPiece> pointed = pieces.FindAll(piece => piece.pointed);
            if (pointed.Count == 0) return;
            var usage = new Dictionary<string, int>();
            float minimumHalf = profile.Length * 0.012f;
            int shardCount = random.RangeInclusive(2, 4);
            for (int shard = 0; shard < shardCount; shard++)
            {
                float share = random.Uniform(0.55f, 0.95f);
                float along = profile.tipSign > 0 ? profile.start + share * profile.Length : profile.end - share * profile.Length;
                profile.At(along, out float middle, out float half);
                half = Mathf.Max(half, minimumHalf);
                int side = random.Sign();
                float angle = random.Uniform(12f, 28f) * Mathf.Deg2Rad;
                Vector2 direction = profile.axis * profile.tipSign * Mathf.Cos(angle) + profile.normal * side * Mathf.Sin(angle);
                float length = profile.Length * random.Uniform(0.1f, 0.17f);
                Vector2 baseCenter = profile.ToImage(along, middle + side * half * 0.6f);

                KitbashPiece piece = LeastUsed(pointed, usage, random);
                PieceFrame frame = piece.Frame(random.Chance(0.5f));
                Vector2 major = frame.major;
                Vector2 minor = frame.minor;
                if (random.Chance(0.5f))
                {
                    major = -major;
                    minor = -minor;
                }
                Affine2D lay = Affine2D.AlongDirection(frame.center, major, minor, frame.length, frame.thickness, baseCenter + direction * length * 0.4f, direction, length, length * random.Uniform(0.18f, 0.28f));
                canvas.DrawPiece(piece, Affine2D.Compose(lay, frame.mirror));
            }
        }

        private static KitbashPiece LeastUsed(List<KitbashPiece> candidates, Dictionary<string, int> usage, KitbashRandom random)
        {
            var weights = new List<float>();
            foreach (KitbashPiece candidate in candidates) weights.Add(1f / (1 + (usage.TryGetValue(candidate.name, out int count) ? count : 0)));
            KitbashPiece piece = random.WeightedChoice(candidates, weights);
            usage[piece.name] = (usage.TryGetValue(piece.name, out int used) ? used : 0) + 1;
            return piece;
        }

        private static PixelMask Swayed(PixelMask shape, KitbashRandom random, int scale)
        {
            int width = shape.width;
            var noise = new float[width];
            for (int column = 0; column < width; column++) noise[column] = random.Uniform(-1f, 1f);
            float[] sway = GaussianSmooth(noise, width * 0.06f);
            float largest = 1e-6f;
            foreach (float value in sway) largest = Mathf.Max(largest, Mathf.Abs(value));
            float amplitude = Mathf.Min(2 * scale, MedianColumnThickness(shape) * 0.1f);

            var swayed = new PixelMask(width, shape.height);
            for (int column = 0; column < width; column++)
            {
                int shift = Mathf.RoundToInt(sway[column] / largest * amplitude);
                for (int row = 0; row < shape.height; row++)
                {
                    int sourceRow = row - shift;
                    if (sourceRow >= 0 && sourceRow < shape.height) swayed.cells[row * width + column] = shape.cells[sourceRow * width + column];
                }
            }
            return swayed;
        }

        private static PixelMask Thickened(PixelMask shape, WeaponProfile profile)
        {
            float typical = Mathf.Max(1f, MedianColumnThickness(shape));
            float factor = Mathf.Clamp(profile.Length * 0.05f / typical, 1f, 2.2f);
            if (factor <= 1f) return shape;

            int width = shape.width;
            int height = shape.height;
            var centres = new float[width];
            for (int column = 0; column < width; column++) centres[column] = ColumnMean(shape, column);
            centres = BoxSmooth(centres, Mathf.Max(3, (int)typical));

            var thickened = new PixelMask(width, height);
            for (int column = 0; column < width; column++)
            {
                for (int row = 0; row < height; row++)
                {
                    float sourceRow = centres[column] + (row - centres[column]) / factor;
                    int upper = Mathf.FloorToInt(sourceRow);
                    float lowerShare = sourceRow - upper;
                    float value = (shape[column, upper] ? 1f - lowerShare : 0f) + (shape[column, upper + 1] ? lowerShare : 0f);
                    thickened.cells[row * width + column] = value > 0.5f;
                }
            }
            return thickened;
        }

        private static float ColumnMean(PixelMask shape, int column)
        {
            float sum = 0f;
            int count = 0;
            for (int row = 0; row < shape.height; row++)
            {
                if (!shape.cells[row * shape.width + column]) continue;
                sum += row;
                count++;
            }
            return count == 0 ? shape.height / 2f : sum / count;
        }

        private static float MedianColumnThickness(PixelMask shape)
        {
            var thicknesses = new List<float>();
            for (int column = 0; column < shape.width; column++)
            {
                int count = 0;
                for (int row = 0; row < shape.height; row++) if (shape.cells[row * shape.width + column]) count++;
                if (count > 0) thicknesses.Add(count);
            }
            if (thicknesses.Count == 0) return 0f;
            thicknesses.Sort();
            return thicknesses[thicknesses.Count / 2];
        }

        private static float[] GaussianSmooth(float[] values, float sigma)
        {
            int radius = Mathf.Max(1, Mathf.CeilToInt(sigma * 4f));
            var weights = new float[radius * 2 + 1];
            for (int offset = -radius; offset <= radius; offset++) weights[offset + radius] = Mathf.Exp(-(offset * offset) / (2f * sigma * sigma));
            var smoothed = new float[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                float sum = 0f, weightSum = 0f;
                for (int offset = -radius; offset <= radius; offset++)
                {
                    int source = index + offset;
                    if (source < 0) source = -source - 1;
                    if (source >= values.Length) source = 2 * values.Length - source - 1;
                    source = Mathf.Clamp(source, 0, values.Length - 1);
                    sum += values[source] * weights[offset + radius];
                    weightSum += weights[offset + radius];
                }
                smoothed[index] = sum / weightSum;
            }
            return smoothed;
        }

        private static float[] BoxSmooth(float[] values, int window)
        {
            var smoothed = new float[values.Length];
            int half = window / 2;
            for (int index = 0; index < values.Length; index++)
            {
                float sum = 0f;
                for (int offset = -half; offset < window - half; offset++) sum += values[Mathf.Clamp(index + offset, 0, values.Length - 1)];
                smoothed[index] = sum / window;
            }
            return smoothed;
        }
    }
}
