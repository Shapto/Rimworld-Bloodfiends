using HarmonyLib;
using SingularityFramework.Kitbash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hardblood
{
    public class HardbloodReplicaGraphic : Graphic_Single
    {
        public static HardbloodReplicaGraphic Create(Material material, Vector2 drawSize, string name)
        {
            var graphic = new HardbloodReplicaGraphic
            {
                drawSize = drawSize,
                color = Color.white,
                path = name
            };
            graphic.mat = material;
            return graphic;
        }
    }

    public static class HardbloodReplicaGraphics
    {
        private const float WeaponPadding = 0.25f;   // room around the weapon, as a share of its size (same as the coating)
        private const int GrowthTicks = 180;         // how long the crystal takes to harden in (3 seconds at normal speed)

        // Thing caches its graphic in this field; setting it makes the replica draw as hardblood everywhere
        private static readonly FieldInfo GraphicField = AccessTools.Field(typeof(Thing), "graphicInt");

        private class ReplicaEntry
        {
            public Graphic graphic;
            public Material material;
            public Texture2D texture;
            public Vector2 grip;   // the weapon's ends on its sprite (-0.5 to 0.5), for the blood lines
            public Vector2 tip;
        }

        private class ReplicaBuild
        {
            public KitbashCanvas canvas;
            public Vector2 grip;   // in canvas pixels
            public Vector2 tip;
        }

        private static readonly Dictionary<int, ReplicaEntry> entriesByReplica = new Dictionary<int, ReplicaEntry>();
        private static Material cachedBlankMaterial;
        private static Game cachedForGame;

        private static Material BlankMaterial
        {
            get
            {
                if (cachedBlankMaterial == null) cachedBlankMaterial = new Material(ShaderDatabase.Cutout) { name = "HardbloodReplica_Blank", mainTexture = BaseContent.ClearTex };
                return cachedBlankMaterial;
            }
        }

        /// <summary>
        /// Gives the replica its look if it doesn't have it: after loading a save (generated textures aren't saved), or
        /// if something made the weapon forget its graphic (a colour change).
        /// </summary>
        public static void EnsureApplied(ThingWithComps replica)
        {
            if (GraphicField == null) return;
            ForgetEverythingIfNewGame();
            if (!entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry))
            {
                Apply(replica, false);
                return;
            }
            if (entry.graphic != null && GraphicField.GetValue(replica) != entry.graphic) GraphicField.SetValue(replica, entry.graphic);
        }

        public static Vector2 GripOf(Thing replica) => entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry) ? entry.grip : new Vector2(-0.3f, -0.3f);

        public static Vector2 TipOf(Thing replica) => entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry) ? entry.tip : new Vector2(0.32f, 0.32f);

        /// <summary>
        /// Makes the replica invisible and starts building its hardblood look, which replaces it when it's done.
        /// "growsIn": the crystal hardens in from the grip to the tip instead of just being there.
        /// </summary>
        public static void Apply(ThingWithComps replica, bool growsIn)
        {
            ForgetEverythingIfNewGame();
            if (entriesByReplica.ContainsKey(replica.thingIDNumber)) return;
            var entry = new ReplicaEntry();
            entriesByReplica[replica.thingIDNumber] = entry;

            Graphic original = replica.def.graphic;
            Texture texture = original?.MatSingle?.mainTexture;
            if (GraphicField == null) Log.ErrorOnce("[Bloodfiends] Thing.graphicInt not found: hardblood replicas will look like the weapon they copy.", 51730211);
            if (texture == null || GraphicField == null) return;
            int width = HardbloodGraphics.CanvasSize;
            int height = Mathf.Max(1, Mathf.RoundToInt(HardbloodGraphics.CanvasSize * texture.height / (float)texture.width));
            Color32[] pixels = KitbashTextures.ReadPixels(texture, width, height, false);

            int padding = Mathf.RoundToInt(Mathf.Max(width, height) * WeaponPadding);
            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;
            var padded = new Color32[paddedWidth * paddedHeight];
            for (int row = 0; row < height; row++) System.Array.Copy(pixels, row * width, padded, (row + padding) * paddedWidth + padding, width);

            // drawn bigger than the weapon by the padding, so the weapon itself stays the same size
            Vector2 drawSize = new Vector2(original.drawSize.x * paddedWidth / width, original.drawSize.y * paddedHeight / height);
            entry.grip = new Vector2(-0.3f * width / paddedWidth, -0.3f * height / paddedHeight);
            entry.tip = new Vector2(0.32f * width / paddedWidth, 0.32f * height / paddedHeight);
            string name = $"HardbloodReplica_{replica.thingIDNumber}";
            entry.graphic = HardbloodReplicaGraphic.Create(BlankMaterial, drawSize, name);
            GraphicField.SetValue(replica, entry.graphic);

            List<KitbashPiece> crystals = BloodfiendsDefOf.Bloodfiends_HardbloodCrystals.Pieces;
            int seed = replica.thingIDNumber;
            bool pointed = IsPointed(replica.def);
            KitbashBuilds.Start(() =>
            {
                var build = new ReplicaBuild();
                build.canvas = HardbloodWeapons.Replica(padded, paddedWidth, paddedHeight, crystals, seed, HardbloodGraphics.Scale, pointed, out build.grip, out build.tip);
                return build;
            },
            build =>
            {
                // the replica may have dissolved (or a new game started) while this was building
                if (!entriesByReplica.TryGetValue(seed, out ReplicaEntry current) || current != entry || replica.Destroyed) return;
                entry.material = KitbashGraphic.SingleMaterial(build.canvas, name, ShaderDatabase.Cutout, out entry.texture, growsIn ? GrowthTicks : 0);
                entry.grip = ToSprite(build.grip, paddedWidth, paddedHeight);
                entry.tip = ToSprite(build.tip, paddedWidth, paddedHeight);
                entry.graphic = HardbloodReplicaGraphic.Create(entry.material, drawSize, name);
                GraphicField.SetValue(replica, entry.graphic);
            },
            name);
        }

        /// <summary>
        /// A canvas pixel (row 0 at the top) as a point on the sprite (-0.5 to 0.5, up is +y).
        /// </summary>
        private static Vector2 ToSprite(Vector2 canvasPoint, int width, int height)
        {
            return new Vector2(canvasPoint.x / width - 0.5f, 0.5f - canvasPoint.y / height);
        }

        /// <summary>
        /// Weapons that stab (spears, swords) get a sharp tip.
        /// </summary>
        private static bool IsPointed(ThingDef weaponDef)
        {
            if (weaponDef.tools == null) return false;
            foreach (Tool tool in weaponDef.tools)
            {
                if (tool.capacities == null) continue;
                foreach (ToolCapacityDef capacity in tool.capacities) if (capacity.defName == "Stab") return true;
            }
            return false;
        }

        /// <summary>
        /// Frees a replica's textures (it dissolved).
        /// </summary>
        public static void Forget(Thing replica)
        {
            if (!entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry)) return;
            Destroy(entry);
            entriesByReplica.Remove(replica.thingIDNumber);
        }

        private static void Destroy(ReplicaEntry entry)
        {
            if (entry.texture != null) KitbashGrowth.Stop(new[] { entry.texture });
            if (entry.material != null) UnityEngine.Object.Destroy(entry.material);
            if (entry.texture != null) UnityEngine.Object.Destroy(entry.texture);
        }

        /// <summary>
        /// Thing IDs start over in every game, so everything built for another game is thrown away.
        /// </summary>
        private static void ForgetEverythingIfNewGame()
        {
            if (cachedForGame == Current.Game) return;
            cachedForGame = Current.Game;
            foreach (ReplicaEntry entry in entriesByReplica.Values) Destroy(entry);
            entriesByReplica.Clear();
        }
    }
}
