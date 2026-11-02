using System.Collections.Generic;
using Bloodfiends.BloodCommand;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using UnityEngine;
using Verse;

namespace Bloodfiends.LaSangre
{
    /// <summary>
    /// The show around the La Sangre duel: night falls over the map and La Manchaland's starry sky opens behind the lock, with
    /// its moon just above where the lances meet. The screen flashes on every
    /// big hit, shockwaves ring out of each surge, the winner's swing leaves a trail and afterimages, and the slam bursts
    /// out of the ground in spikes. None of it touches the game; it's all drawn or spawned as effects.
    /// </summary>
    public partial class LaSangreFlight
    {
        private const float NightDarkness = 0.92f;
        private const float MoonRise = 0.6f;
        private const int StarTextureWidth = 768;
        private const int StarTextureHeight = 256;
        private const int StarCount = 70;
        private const float TwinkleSpeed = 2.2f;
        private const int AfterimageCount = 3;
        private const int AfterimageSpacingTicks = 2;
        private const int SurgeFlashTicks = 6;
        private const int ImpactFlashTicks = 12;
        private const int BreakFlashTicks = 18;
        private const int SlamFlashTicks = 16;
        private const int CollisionLeadTicks = 15;
        private const int GrindDelayTicks = 8;
        private const int FireworkDelayTicks = 12;
        private const int FlowIntervalTicks = 2;
        private static readonly FloatRange FlowLength = new FloatRange(3.5f, 7f);
        private static readonly FloatRange FlowWidth = new FloatRange(0.06f, 0.22f);
        private const float FlowSpread = 0.35f;
        private const float FireworkInnerRadius = 2.5f;
        private const float FireworkOuterRadius = 6f;

        /// <summary>
        /// When each firework bursts, in ticks after the firework sound starts: on the crackles in Firework.wav.
        /// </summary>
        private static readonly int[] FireworkBurstTicks = { 3, 14, 25, 34, 45, 62, 75 };

        private static readonly BrushPalette GlowPalette = new BrushPalette(new Color(1f, 0.82f, 0.78f), new Color(0.95f, 0.2f, 0.16f), new Color(0.55f, 0.03f, 0.05f));
        private static readonly Color NightColor = new Color(0.01f, 0f, 0.05f);
        private static readonly Color WhiteFlashColor = new Color(1f, 0.96f, 0.94f);
        private static readonly Color RedFlashColor = new Color(0.85f, 0.05f, 0.05f);
        private static readonly Color DustColor = new Color(0.35f, 0.29f, 0.24f);
        private static readonly BrushPalette FireworkPalette = new BrushPalette(new Color(1f, 0.55f, 0.5f), new Color(0.9f, 0.12f, 0.12f), new Color(0.6f, 0.05f, 0.1f));

        private static Material nightMaterial;
        private static Material whiteFlashMaterial;
        private static Material redFlashMaterial;
        private static Material skyMaterial;
        private static Material[] twinkleMaterials;

        /// <summary>
        /// The duel's game-side moments: effects started on the tick they happen, so they play even when nobody's looking.
        /// </summary>
        private void TickSpectacle(Map map)
        {
            TickDuelSounds(map);
            if (ageTicks == DiveTick)
            {
                MapComponent_BrushLines.Start(map, new SmearLineEffect(MapComponent_BloodCommand.BloodPalette, airPosition, Contact(0) - diveDirection * Reach, seed + 51, 0.45f));
                MapComponent_BrushLines.Start(map, new SmearLineEffect(MapComponent_BloodCommand.BloodPalette, duelistGround, Contact(0) + diveDirection * Reach, seed + 52, 0.45f));
            }
            if (ageTicks == ResolveTick)
            {
                Vector3 contact = Contact(Props.duelClashes);
                MapComponent_BrushLines.Start(map, new RingLineEffect(GlowPalette, contact, 0.5f, 5.5f, 0.24f, 10, seed + 61, 0.8f));
                MapComponent_BrushLines.Start(map, new BurstLineEffect(GlowPalette, contact, 30, new FloatRange(1.5f, 3.5f), new FloatRange(0.12f, 0.22f), seed + 62, 0.5f));
                FleckMaker.Static(contact, map, FleckDefOf.ExplosionFlash, FirstImpactFlashSize * 1.5f);
                Shake(map, FirstImpactShake);
            }
            if (ageTicks == TableauEndTick)
            {
                List<Vector3> swingPath = new List<Vector3>();
                for (int tick = TableauEndTick; tick <= SlamTick; tick++) swingPath.Add(AtTick(tick, () => LoserPlace().position));
                MapComponent_BrushLines.Start(map, new TrailLineEffect(MapComponent_BloodCommand.BloodPalette, swingPath, 0.9f, 0.35f, seed + 71, 1f));
            }
            if (ageTicks == SlamTick) SlamSpectacle(map);
            TickFlowLines(map);
        }

        /// <summary>
        /// The duel's sounds, cut from the cutscene's: some start early so their hit lands on the beat.
        /// </summary>
        private void TickDuelSounds(Map map)
        {
            if (ageTicks == DiveTick) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelNight, meetPoint, map);
            if (ageTicks == FirstClashTick - CollisionLeadTicks) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelCollision, Contact(0), map);
            if (ageTicks == FirstClashTick + GrindDelayTicks) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelGrind, Contact(0), map);
            if (ageTicks == ResolveTick) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelBreakthrough, Contact(Props.duelClashes), map);
            if (ageTicks == TableauEndTick) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelSwing, ImpaledSpot, map);
            if (ageTicks == SlamTick) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelSlam, SlamPoint, map);
            if (ageTicks == FirstClashTick + FireworkDelayTicks) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelFirework, meetPoint, map);
            TickFireworks(map);
        }

        /// <summary>
        /// Once the lances lock under the moon, fireworks burst in the sky around it, each on a crackle of the sound.
        /// </summary>
        private void TickFireworks(Map map)
        {
            int burstIndex = System.Array.IndexOf(FireworkBurstTicks, ageTicks - FirstClashTick - FireworkDelayTicks);
            if (burstIndex < 0) return;
            var random = new System.Random(seed + 121 + burstIndex);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(FireworkInnerRadius, FireworkOuterRadius, (float)random.NextDouble());
            Vector3 burstCentre = meetPoint + new Vector3(0f, 0f, MoonRise) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            float size = Mathf.Lerp(0.7f, 1.4f, (float)random.NextDouble());
            MapComponent_BrushLines.Start(map, new BurstLineEffect(FireworkPalette, burstCentre, 16, new FloatRange(size * 0.6f, size), new FloatRange(0.04f, 0.08f), random.Next(), 0.8f));
            FleckMaker.ThrowMicroSparks(burstCentre, map);
            FleckMaker.Static(burstCentre, map, FleckDefOf.ShotFlash, size * 1.5f);
        }

        /// <summary>
        /// Streaks of blood pouring out of the duel's centre and flowing back past each duelist along the line of the lances,
        /// for as long as they're locked; once the winner breaks through, only behind the winner, until they hang still.
        /// </summary>
        private void TickFlowLines(Map map)
        {
            if (ageTicks < FirstClashTick || ageTicks >= ImpaledTick || (ageTicks - FirstClashTick) % FlowIntervalTicks != 0) return;
            Vector3 centre = ageTicks < ResolveTick ? LockContact() : Contact(Props.duelClashes);
            if (!duelResolved || casterWonDuel) StartFlowLine(map, centre, -diveDirection, 0);
            if (!duelResolved || !casterWonDuel) StartFlowLine(map, centre, diveDirection, 1);
        }

        private void StartFlowLine(Map map, Vector3 centre, Vector3 backward, int side)
        {
            var random = new System.Random(seed + 131 + ageTicks * 2 + side);
            Vector3 across = new Vector3(-backward.z, 0f, backward.x);
            float offset = ((float)random.NextDouble() * 2f - 1f) * FlowSpread;
            Vector3 start = centre + backward * (0.3f + (float)random.NextDouble() * 0.8f) + across * offset;
            float length = Mathf.Lerp(FlowLength.min, FlowLength.max, (float)random.NextDouble());
            float width = Mathf.Lerp(FlowWidth.min, FlowWidth.max, (float)random.NextDouble());
            BrushPalette palette = random.NextDouble() < 0.2 ? GlowPalette : MapComponent_BloodCommand.BloodPalette;
            MapComponent_BrushLines.Start(map, new FlowLineEffect(palette, start, backward, length, offset * 1.5f, width, random.Next(), 0.45f));
        }

        private void SurgeSpectacle(Map map, Vector3 contact, bool firstImpact)
        {
            MapComponent_BrushLines.Start(map, new RingLineEffect(firstImpact ? GlowPalette : MapComponent_BloodCommand.BloodPalette, contact, 0.3f, firstImpact ? 4f : 2.2f, firstImpact ? 0.2f : 0.12f, firstImpact ? 8 : 6, seed + 81 + ageTicks, firstImpact ? 0.7f : 0.45f));
            for (int spark = 0; spark < (firstImpact ? 6 : 3); spark++) FleckMaker.ThrowMicroSparks(contact, map);
        }

        private void SlamSpectacle(Map map)
        {
            Vector3 slamPoint = SlamPoint;
            MapComponent_BrushLines.Start(map, new RingLineEffect(GlowPalette, slamPoint, 0.5f, 6f, 0.26f, 12, seed + 91, 0.9f));
            MapComponent_BrushLines.Start(map, new RingLineEffect(MapComponent_BloodCommand.BloodPalette, slamPoint, 0.3f, 3.5f, 0.4f, 8, seed + 92, 1.1f));
            MapComponent_BrushLines.Start(map, new SpikeLineEffect(MapComponent_BloodCommand.BloodPalette, slamPoint, 16, new FloatRange(0.4f, 1.8f), new FloatRange(1.2f, 2.8f), new FloatRange(0.12f, 0.24f), seed + 93, 1.2f));
            FleckMaker.Static(slamPoint, map, FleckDefOf.ExplosionFlash, SlamFlashSize * 1.6f);
            for (int puff = 0; puff < 10; puff++) FleckMaker.ThrowDustPuffThick(slamPoint + Rand.InsideUnitCircleVec3 * 1.5f, map, Rand.Range(1.5f, 2.5f), DustColor);
            Shake(map, Props.cameraShake * 0.5f);
        }

        /// <summary>
        /// Reads something about the duel as it was (or will be) at another tick: the duel's choreography is a pure function of its age.
        /// </summary>
        private T AtTick<T>(int tick, System.Func<T> read)
        {
            int actualAge = ageTicks;
            ageTicks = tick;
            try
            {
                return read();
            }
            finally
            {
                ageTicks = actualAge;
            }
        }

        private void DrawDuelSpectacle(float airAltitude)
        {
            EnsureSpectacleMaterials();
            float night = NightShare();
            if (night > 0f)
            {
                DrawOverView(AltitudeLayer.PawnUnused.AltitudeFor(), nightMaterial, night * NightDarkness);
                DrawSky(night);
            }
            DrawSwingAfterimages(airAltitude);

            (float flash, bool redFlash) = Flash();
            if (flash > 0f) DrawOverView(AltitudeLayer.MetaOverlays.AltitudeFor(), redFlash ? redFlashMaterial : whiteFlashMaterial, flash);
        }

        /// <summary>
        /// Night falls as the two rush at each other and lifts as the winner comes down.
        /// </summary>
        private float NightShare()
        {
            if (ageTicks < DiveTick) return 0f;
            float fallen = BrushStroke.SmoothStep(DiveTick, FirstClashTick, ageTicks);
            return fallen * (1f - BrushStroke.SmoothStep(SlamTick, LandTick, ageTicks));
        }

        /// <summary>
        /// The brightest flash going on right now: white for the first collision, each surge and the breakthrough, red for the slam.
        /// </summary>
        private (float alpha, bool red) Flash()
        {
            float white = Mathf.Max(FlashFrom(FirstClashTick, 0.8f, ImpactFlashTicks), FlashFrom(ResolveTick, 0.95f, BreakFlashTicks));
            for (int clashIndex = 1; clashIndex < Props.duelClashes; clashIndex++) white = Mathf.Max(white, FlashFrom(FirstClashTick + clashIndex * ClashIntervalTicks, 0.3f, SurgeFlashTicks));
            float red = FlashFrom(SlamTick, 0.6f, SlamFlashTicks);
            return red > white ? (red, true) : (white, false);
        }

        private float FlashFrom(int flashTick, float strength, int fadeTicks)
        {
            int since = ageTicks - flashTick;
            if (since < 0 || since >= fadeTicks) return 0f;
            return strength * Mathf.Pow(1f - since / (float)fadeTicks, 2f);
        }

        /// <summary>
        /// The winner's lance leaves fading copies of itself behind as it swings the loser down.
        /// </summary>
        private void DrawSwingAfterimages(float airAltitude)
        {
            if (!duelResolved || ageTicks < TableauEndTick || ageTicks > SlamTick + AfterimageCount * AfterimageSpacingTicks) return;
            bool winnerIsCaster = casterWonDuel;
            for (int afterimage = 1; afterimage <= AfterimageCount; afterimage++)
            {
                int tick = Mathf.Min(ageTicks - afterimage * AfterimageSpacingTicks, SlamTick);
                if (tick < TableauEndTick) break;
                (Vector3 position, float height) = AtTick(tick, () => DuelPlace(winnerIsCaster));
                Vector3 direction = AtTick(tick, ImpaleDirection);
                enhancedLanceLook.Draw(position, direction, 1f, LanceAltitude(height, airAltitude) - 0.01f * afterimage, 0.5f / afterimage);
            }
        }

        /// <summary>
        /// Covers the whole view in one colour, faded to "alpha". Fading goes through vanilla's faded material pool, one material
        /// per fade step, so every quad drawn in a frame keeps its own fade.
        /// </summary>
        private static void DrawOverView(float altitude, Material material, float alpha)
        {
            CellRect view = Find.CameraDriver.CurrentViewRect;
            Vector3 centre = view.CenterVector3.WithY(altitude);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(centre, Quaternion.identity, new Vector3(view.Width + 4f, 1f, view.Height + 4f)), FadedMaterialPool.FadedVersionOf(material, alpha), 0);
        }

        /// <summary>
        /// La Manchaland's night sky, laid behind the duel so its moon sits just above the lock, with two layers of stars
        /// fading in and out of step on top so the sky twinkles.
        /// </summary>
        private void DrawSky(float night)
        {
            Texture skyTexture = skyMaterial.mainTexture;
            float skyWidth = Props.duelSkyWidth;
            float skyHeight = skyWidth * skyTexture.height / skyTexture.width;
            Vector3 moonCentre = meetPoint + new Vector3(0f, 0f, MoonRise);
            Vector2 moonOnTexture = Props.duelSkyMoonOnTexture;
            Vector3 skyCentre = moonCentre + new Vector3((0.5f - moonOnTexture.x) * skyWidth, 0f, (0.5f - moonOnTexture.y) * skyHeight);
            Vector3 skySize = new Vector3(skyWidth, 1f, skyHeight);
            float skyAltitude = AltitudeLayer.PawnState.AltitudeFor();

            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(skyCentre.WithY(skyAltitude), Quaternion.identity, skySize), FadedMaterialPool.FadedVersionOf(skyMaterial, night), 0);
            for (int layer = 0; layer < twinkleMaterials.Length; layer++)
            {
                float twinkle = (Mathf.Sin(Time.realtimeSinceStartup * TwinkleSpeed + layer * Mathf.PI) + 1f) / 2f;
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(skyCentre.WithY(skyAltitude + 0.01f + layer * 0.001f), Quaternion.identity, skySize), FadedMaterialPool.FadedVersionOf(twinkleMaterials[layer], night * twinkle), 0);
            }
        }

        private void EnsureSpectacleMaterials()
        {
            if (nightMaterial != null) return;
            nightMaterial = SolidColorOverlay(NightColor);
            whiteFlashMaterial = SolidColorOverlay(WhiteFlashColor);
            redFlashMaterial = SolidColorOverlay(RedFlashColor);
            Texture2D skyTexture = ContentFinder<Texture2D>.Get(Props.duelSkyTexturePath, false) ?? BaseContent.ClearTex;
            skyMaterial = MaterialPool.MatFrom(new MaterialRequest(skyTexture, ShaderDatabase.Transparent, Color.white));
            twinkleMaterials = new[] { MakeStarsMaterial(41), MakeStarsMaterial(97) };
        }

        /// <summary>
        /// A see-through material of one colour, baked into its texture so fading it keeps the colour.
        /// </summary>
        private static Material SolidColorOverlay(Color color) => MaterialPool.MatFrom(new MaterialRequest(SolidColorMaterials.NewSolidColorTexture(color), ShaderDatabase.Transparent, Color.white));

        /// <summary>
        /// A layer of small soft stars in the sky's colours, made once at runtime; it fades out toward the sky's edges like the sky does.
        /// </summary>
        private static Material MakeStarsMaterial(int starSeed)
        {
            var texture = new Texture2D(StarTextureWidth, StarTextureHeight, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[StarTextureWidth * StarTextureHeight];
            Color[] starColors = { new Color(1f, 1f, 1f), new Color(0.75f, 0.8f, 1f), new Color(1f, 0.75f, 0.9f), new Color(1f, 0.95f, 0.7f) };
            var random = new System.Random(starSeed);
            for (int star = 0; star < StarCount; star++)
            {
                float starX = (float)random.NextDouble() * StarTextureWidth;
                float starY = StarTextureHeight * (0.3f + 0.65f * (float)random.NextDouble());
                float radius = 1.2f + 2f * (float)random.NextDouble();
                Color starColor = starColors[random.Next(starColors.Length)];
                float edgeFade = Mathf.Clamp01(Mathf.Min(starX, StarTextureWidth - starX) / (StarTextureWidth * 0.15f));
                for (int row = Mathf.Max(0, (int)(starY - radius * 2f)); row < Mathf.Min(StarTextureHeight, (int)(starY + radius * 2f) + 1); row++)
                {
                    for (int column = Mathf.Max(0, (int)(starX - radius * 2f)); column < Mathf.Min(StarTextureWidth, (int)(starX + radius * 2f) + 1); column++)
                    {
                        float distance = new Vector2(column - starX, row - starY).magnitude / radius;
                        float glow = Mathf.Clamp01(1f - distance / 2f);
                        glow *= glow * edgeFade;
                        int index = row * StarTextureWidth + column;
                        if (glow <= pixels[index].a) continue;
                        pixels[index] = new Color(starColor.r, starColor.g, starColor.b, glow);
                    }
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, Color.white));
        }
    }
}
