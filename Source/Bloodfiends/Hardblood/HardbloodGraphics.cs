using SingularityFramework.Kitbash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hardblood
{
    public static class HardbloodGraphics
    {
        public const int CanvasSize = 512;           // head and body textures are read at this size (128 pixel textures at 4x)
        public const int Scale = 4;                  // canvas pixels per pixel of a 128 pixel texture
        private const float WeaponPadding = 0.25f;   // room around a weapon for shards sweeping past it, as a share of its size
        private const int GrowthTicks = 180;         // how long growths take to grow in (180 ticks: 3 seconds at normal speed)
        private const int NewGrowthAgeTicks = 600;   // growths on a pawn younger than this grow in; older ones (loaded saves) are just there

        private class GrowthEntry
        {
            public KitbashGraphic head;
            public KitbashGraphic body;
        }

        private class CoatingEntry
        {
            public Material material;
            public Texture2D texture;
            public Vector2 drawScale;
        }

        private static readonly Dictionary<int, GrowthEntry> growthsByPawn = new Dictionary<int, GrowthEntry>();
        private static readonly Dictionary<int, CoatingEntry> coatingsByWeapon = new Dictionary<int, CoatingEntry>();
        private static Game cachedForGame;

        private static List<KitbashPiece> Crystals => BloodfiendsDefOf.Bloodfiends_HardbloodCrystals.Pieces;

        // ----- Bloodbag growths -----

        /// <summary>
        /// The growths' graphic for a pawn's head or body. Null until it's built (the first call starts the build).
        /// </summary>
        public static Graphic GrowthGraphic(Pawn pawn, bool onHead)
        {
            ForgetEverythingIfNewGame();
            if (!growthsByPawn.TryGetValue(pawn.thingIDNumber, out GrowthEntry entry))
            {
                entry = new GrowthEntry();
                growthsByPawn[pawn.thingIDNumber] = entry;
                StartGrowthBuild(pawn, entry);
            }
            return onHead ? entry.head : entry.body;
        }

        private static void StartGrowthBuild(Pawn pawn, GrowthEntry entry)
        {
            Graphic_Multi headGraphic = pawn.story?.headType?.GetGraphic(pawn, Color.white);
            string bodyPath = pawn.story?.bodyType?.bodyNakedGraphicPath;
            if (headGraphic == null || bodyPath == null) return;   // only humanlikes have the head and body to grow on
            Graphic_Multi bodyGraphic = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(bodyPath, ShaderDatabase.CutoutSkin, Vector2.one, Color.white);

            // textures can only be read on the main thread, so everything the build needs is read now
            PixelMask[] headSilhouettes = ReadFacings(headGraphic);
            PixelMask[] bodySilhouettes = ReadFacings(bodyGraphic);
            List<KitbashPiece> crystals = Crystals;
            int seed = pawn.thingIDNumber;
            string name = $"HardbloodGrowths_{pawn.thingIDNumber}";
            Hediff growthsHediff = pawn.health.hediffSet.GetFirstHediffOfDef(BloodfiendsDefOf.Bloodfiends_HardbloodGrowths);
            int growthTicks = growthsHediff != null && growthsHediff.ageTicks < NewGrowthAgeTicks ? GrowthTicks : 0;

            KitbashBuilds.Start(() =>
            {
                var growths = new BloodbagGrowths(headSilhouettes[Rot4.South.AsInt], bodySilhouettes[Rot4.South.AsInt], crystals, seed, Scale);
                var headCanvases = new KitbashCanvas[4];
                var bodyCanvases = new KitbashCanvas[4];
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    headCanvases[rotation] = growths.HeadFacing(headSilhouettes[rotation], rotation);
                    bodyCanvases[rotation] = growths.BodyFacing(bodySilhouettes[rotation], rotation);
                }
                return new[] { headCanvases, bodyCanvases };
            },
            canvases =>
            {
                // the pawn may have been forgotten (hediff removed, new game) while this was building
                if (!growthsByPawn.TryGetValue(seed, out GrowthEntry current) || current != entry) return;
                entry.head = KitbashGraphic.Create(canvases[0], name + "_Head", ShaderDatabase.Cutout, Vector2.one, growthTicks);
                entry.body = KitbashGraphic.Create(canvases[1], name + "_Body", ShaderDatabase.Cutout, Vector2.one, growthTicks);
                if (!pawn.Destroyed) pawn.Drawer.renderer.SetAllGraphicsDirty();
            },
            name);
        }

        /// <summary>
        /// A graphic's silhouette for each facing, indexed by Rot4.AsInt. West is the east texture flipped when the graphic
        /// has no west texture of its own.
        /// </summary>
        private static PixelMask[] ReadFacings(Graphic_Multi graphic)
        {
            var silhouettes = new PixelMask[4];
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var facing = new Rot4(rotation);
                bool mirrored = facing == Rot4.West && graphic.WestFlipped;
                silhouettes[rotation] = KitbashTextures.ReadSilhouette(graphic.MatAt(facing).mainTexture, CanvasSize, CanvasSize, mirrored);
            }
            return silhouettes;
        }

        /// <summary>
        /// Drops a pawn's growths (the hediff was removed), freeing their textures.
        /// </summary>
        public static void Forget(Pawn pawn)
        {
            if (!growthsByPawn.TryGetValue(pawn.thingIDNumber, out GrowthEntry entry)) return;
            entry.head?.Destroy();
            entry.body?.Destroy();
            growthsByPawn.Remove(pawn.thingIDNumber);
        }

        // ----- weapon coating -----

        /// <summary>
        /// The weapon overlay for Bloodbags: a Bloodbag's weapon is always wrapped in hardblood.
        /// </summary>
        public static bool BloodbagCoating(Pawn pawn, Thing weapon, out Material material, out Vector2 drawScale)
        {
            material = null;
            drawScale = Vector2.one;
            if (!pawn.health.hediffSet.HasHediff(BloodfiendsDefOf.Bloodfiends_HardbloodGrowths)) return false;
            return TryGetCoating(weapon, out material, out drawScale);
        }

        /// <summary>
        /// The coating to draw over a weapon, and how much bigger than the weapon to draw it, across and up (it has room
        /// around it for shards). False until it's built (the first call starts the build).
        /// </summary>
        public static bool TryGetCoating(Thing weapon, out Material material, out Vector2 drawScale)
        {
            ForgetEverythingIfNewGame();
            material = null;
            drawScale = Vector2.one;
            if (!coatingsByWeapon.TryGetValue(weapon.thingIDNumber, out CoatingEntry entry))
            {
                entry = new CoatingEntry();
                coatingsByWeapon[weapon.thingIDNumber] = entry;
                StartCoatingBuild(weapon, entry);
            }
            if (entry.material == null) return false;
            material = entry.material;
            drawScale = entry.drawScale;
            return true;
        }

        private static void StartCoatingBuild(Thing weapon, CoatingEntry entry)
        {
            Texture texture = weapon.Graphic?.MatSingleFor(weapon)?.mainTexture;
            if (texture == null) return;
            int width = CanvasSize;
            int height = Mathf.Max(1, Mathf.RoundToInt(CanvasSize * texture.height / (float)texture.width));
            Color32[] pixels = KitbashTextures.ReadPixels(texture, width, height, false);

            // room around the weapon, so shards sweeping past it aren't cut off at the texture's edge
            int padding = Mathf.RoundToInt(Mathf.Max(width, height) * WeaponPadding);
            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;
            var padded = new Color32[paddedWidth * paddedHeight];
            for (int row = 0; row < height; row++) System.Array.Copy(pixels, row * width, padded, (row + padding) * paddedWidth + padding, width);

            List<KitbashPiece> crystals = Crystals;
            int seed = weapon.thingIDNumber;
            string name = $"HardbloodCoating_{weapon.thingIDNumber}";
            int growthTicks = GameComponent_HardbloodCoatings.Instance?.MarkCoated(weapon) == true ? GrowthTicks : 0;
            KitbashBuilds.Start(() => HardbloodWeapons.Coating(padded, paddedWidth, paddedHeight, crystals, seed, Scale),
            canvas =>
            {
                if (!coatingsByWeapon.TryGetValue(seed, out CoatingEntry current) || current != entry) return;
                entry.material = KitbashGraphic.SingleMaterial(canvas, name, ShaderDatabase.Cutout, out entry.texture, growthTicks);
                entry.drawScale = new Vector2(paddedWidth / (float)width, paddedHeight / (float)height);
            },
            name);
        }

        // ----- housekeeping -----

        /// <summary>
        /// Thing IDs start over in every game, so everything built for another game is thrown away.
        /// </summary>
        private static void ForgetEverythingIfNewGame()
        {
            if (cachedForGame == Current.Game) return;
            cachedForGame = Current.Game;
            foreach (GrowthEntry entry in growthsByPawn.Values)
            {
                entry.head?.Destroy();
                entry.body?.Destroy();
            }
            foreach (CoatingEntry entry in coatingsByWeapon.Values)
            {
                if (entry.texture != null) KitbashGrowth.Stop(new[] { entry.texture });
                if (entry.material != null) UnityEngine.Object.Destroy(entry.material);
                if (entry.texture != null) UnityEngine.Object.Destroy(entry.texture);
            }
            growthsByPawn.Clear();
            coatingsByWeapon.Clear();
        }
    }
}
