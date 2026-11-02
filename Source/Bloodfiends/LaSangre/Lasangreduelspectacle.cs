using System.Collections.Generic;
using Bloodfiends.BloodCommand;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using UnityEngine;
using Verse;

namespace Bloodfiends.LaSangre
{
    /// <summary>
    /// The show around the La Sangre duel: night falls over the map with a moon behind the lock, the screen flashes on every
    /// big hit, shockwaves ring out of each surge, the winner's swing leaves a trail and afterimages, and the slam bursts
    /// out of the ground in spikes. None of it touches the game; it's all drawn or spawned as effects.
    /// </summary>
    public partial class LaSangreFlight
    {
        private const float NightDarkness = 0.6f;
        private const float MoonDiameter = 7f;
        private const float MoonRise = 0.6f;
        private const int MoonTextureSize = 256;
        private const int AfterimageCount = 3;
        private const int AfterimageSpacingTicks = 2;
        private const int SurgeFlashTicks = 6;
        private const int ImpactFlashTicks = 12;
        private const int BreakFlashTicks = 18;
        private const int SlamFlashTicks = 16;
        private const int CollisionLeadTicks = 15;
        private const int GrindDelayTicks = 8;

        private static readonly BrushPalette GlowPalette = new BrushPalette(new Color(1f, 0.82f, 0.78f), new Color(0.95f, 0.2f, 0.16f), new Color(0.55f, 0.03f, 0.05f));
        private static readonly Color NightColor = new Color(0.04f, 0.01f, 0.1f);
        private static readonly Color WhiteFlashColor = new Color(1f, 0.96f, 0.94f);
        private static readonly Color RedFlashColor = new Color(0.85f, 0.05f, 0.05f);
        private static readonly Color DustColor = new Color(0.35f, 0.29f, 0.24f);

        private static Material duelOverlayMaterial;
        private static Material moonMaterial;
        private static MaterialPropertyBlock duelPropertyBlock;

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
                DrawOverView(AltitudeLayer.PawnUnused.AltitudeFor(), new Color(NightColor.r, NightColor.g, NightColor.b, night * NightDarkness));
                Vector3 moonCentre = meetPoint + new Vector3(0f, 0f, MoonRise);
                duelPropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, night));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(moonCentre.WithY(AltitudeLayer.PawnState.AltitudeFor()), Quaternion.identity, new Vector3(MoonDiameter, 1f, MoonDiameter)), moonMaterial, 0, null, 0, duelPropertyBlock);
            }
            DrawSwingAfterimages(airAltitude);

            (float flash, Color flashColor) = Flash();
            if (flash > 0f) DrawOverView(AltitudeLayer.MetaOverlays.AltitudeFor(), new Color(flashColor.r, flashColor.g, flashColor.b, flash));
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
        private (float alpha, Color color) Flash()
        {
            float white = Mathf.Max(FlashFrom(FirstClashTick, 0.8f, ImpactFlashTicks), FlashFrom(ResolveTick, 0.95f, BreakFlashTicks));
            for (int clashIndex = 1; clashIndex < Props.duelClashes; clashIndex++) white = Mathf.Max(white, FlashFrom(FirstClashTick + clashIndex * ClashIntervalTicks, 0.3f, SurgeFlashTicks));
            float red = FlashFrom(SlamTick, 0.6f, SlamFlashTicks);
            return red > white ? (red, RedFlashColor) : (white, WhiteFlashColor);
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

        private void DrawOverView(float altitude, Color color)
        {
            CellRect view = Find.CameraDriver.CurrentViewRect;
            Vector3 centre = view.CenterVector3.WithY(altitude);
            duelPropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(centre, Quaternion.identity, new Vector3(view.Width + 4f, 1f, view.Height + 4f)), duelOverlayMaterial, 0, null, 0, duelPropertyBlock);
        }

        private static void EnsureSpectacleMaterials()
        {
            if (duelOverlayMaterial != null) return;
            duelPropertyBlock = new MaterialPropertyBlock();
            duelOverlayMaterial = MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex, ShaderDatabase.Transparent, Color.white));
            moonMaterial = MaterialPool.MatFrom(new MaterialRequest(MakeMoonTexture(), ShaderDatabase.Transparent, Color.white));
        }

        /// <summary>
        /// A pale moon with a soft halo, made once at runtime.
        /// </summary>
        private static Texture2D MakeMoonTexture()
        {
            var texture = new Texture2D(MoonTextureSize, MoonTextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float half = MoonTextureSize / 2f;
            for (int row = 0; row < MoonTextureSize; row++)
            {
                for (int column = 0; column < MoonTextureSize; column++)
                {
                    float distance = new Vector2(column + 0.5f - half, row + 0.5f - half).magnitude / half;
                    float body = 1f - BrushStroke.SmoothStep(0.6f, 0.63f, distance);
                    float halo = (1f - BrushStroke.SmoothStep(0.6f, 1f, distance)) * 0.35f;
                    float shade = 0.82f + 0.18f * Mathf.PerlinNoise(column * 0.025f, row * 0.025f);
                    Color bodyColor = new Color(0.93f * shade, 0.92f * shade, 1f * shade);
                    Color haloColor = new Color(0.72f, 0.66f, 1f);
                    Color color = Color.Lerp(haloColor, bodyColor, body);
                    color.a = Mathf.Max(body, halo);
                    texture.SetPixel(column, row, color);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
