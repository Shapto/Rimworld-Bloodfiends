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
        private const int RetireFrames = 3;          // a dissolved replica's textures live this many frames longer (this frame's gizmos still draw its icon)

        private const float OutlineWidth = 6f;                                          // canvas pixels: 1.5 pixels of a 128 pixel weapon texture
        private static readonly Color32 OutlineColor = new Color32(18, 8, 10, 255);     // near-black with a hint of red, like vanilla's outlines

        // Thing caches its graphic in this field; setting it makes the replica draw as hardblood everywhere
        private static readonly FieldInfo GraphicField = AccessTools.Field(typeof(Thing), "graphicInt");

        private class ReplicaEntry
        {
            public Graphic graphic;
            public Material material;
            public Texture2D texture;
            public Vector2 grip;   // the weapon's ends on its sprite (-0.5 to 0.5), for the blood lines
            public Vector2 tip;
            public int retiredFrame;
        }

        private class ReplicaBuild
        {
            public KitbashCanvas canvas;
            public Vector2 grip;   // in canvas pixels
            public Vector2 tip;
        }

        private static readonly Dictionary<int, ReplicaEntry> entriesByReplica = new Dictionary<int, ReplicaEntry>();
        private static readonly List<ReplicaEntry> retired = new List<ReplicaEntry>();
        private static Game cachedForGame;

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

        public static Vector2 GripOf(Thing replica) => replica.def.GetModExtension<HardbloodLanceExtension>()?.gripOnSprite ?? (entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry) ? entry.grip : new Vector2(-0.3f, -0.3f));

        public static Vector2 TipOf(Thing replica) => replica.def.GetModExtension<HardbloodLanceExtension>()?.tipOnSprite ?? (entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry) ? entry.tip : new Vector2(0.32f, 0.32f));

        /// <summary>
        /// Gives the replica its (still empty) hardblood texture and starts building the crystal into it.
        /// "growsIn": the crystal hardens in from the grip to the tip instead of just being there.
        /// </summary>
        public static void Apply(ThingWithComps replica, bool growsIn)
        {
            ForgetEverythingIfNewGame();
            if (entriesByReplica.ContainsKey(replica.thingIDNumber)) return;
            var entry = new ReplicaEntry();
            entriesByReplica[replica.thingIDNumber] = entry;

            Graphic original = replica.def.graphic;
            Texture originalTexture = original?.MatSingle?.mainTexture;
            if (GraphicField == null) Log.ErrorOnce("[Bloodfiends] Thing.graphicInt not found: hardblood replicas will look like the weapon they copy.", 51730211);
            if (originalTexture == null || GraphicField == null) return;
            int width = HardbloodGraphics.CanvasSize;
            int height = Mathf.Max(1, Mathf.RoundToInt(HardbloodGraphics.CanvasSize * originalTexture.height / (float)originalTexture.width));
            Color32[] pixels = KitbashTextures.ReadPixels(originalTexture, width, height, false);

            int padding = Mathf.RoundToInt(Mathf.Max(width, height) * WeaponPadding);
            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;
            var padded = new Color32[paddedWidth * paddedHeight];
            for (int row = 0; row < height; row++) System.Array.Copy(pixels, row * width, padded, (row + padding) * paddedWidth + padding, width);

            // the texture, material and graphic the replica keeps for good: empty now, filled in when the build is done
            string name = $"HardbloodReplica_{replica.thingIDNumber}";
            entry.texture = new Texture2D(paddedWidth, paddedHeight, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            entry.texture.SetPixels32(new Color32[paddedWidth * paddedHeight]);
            entry.texture.Apply(true, false);
            entry.material = new Material(ShaderDatabase.Cutout)
            {
                name = name,
                mainTexture = entry.texture,
                color = Color.white
            };
            // drawn bigger than the weapon by the padding, so the weapon itself stays the same size
            Vector2 drawSize = new Vector2(original.drawSize.x * paddedWidth / width, original.drawSize.y * paddedHeight / height);
            entry.graphic = HardbloodReplicaGraphic.Create(entry.material, drawSize, name);
            entry.grip = new Vector2(-0.3f * width / paddedWidth, -0.3f * height / paddedHeight);
            entry.tip = new Vector2(0.32f * width / paddedWidth, 0.32f * height / paddedHeight);
            GraphicField.SetValue(replica, entry.graphic);

            List<KitbashPiece> crystals = BloodfiendsDefOf.Bloodfiends_HardbloodCrystals.Pieces;
            int seed = replica.thingIDNumber;
            bool pointed = IsPointed(replica.def);
            KitbashBuilds.Start(() =>
            {
                var build = new ReplicaBuild();
                build.canvas = HardbloodWeapons.Replica(padded, paddedWidth, paddedHeight, crystals, seed, HardbloodGraphics.Scale, pointed, out build.grip, out build.tip);
                build.canvas.AddOutline(OutlineWidth, OutlineColor);
                return build;
            },
            build =>
            {
                // the replica may have dissolved (or a new game started) while this was building
                if (!entriesByReplica.TryGetValue(seed, out ReplicaEntry current) || current != entry || replica.Destroyed) return;
                entry.grip = ToSprite(build.grip, paddedWidth, paddedHeight);
                entry.tip = ToSprite(build.tip, paddedWidth, paddedHeight);
                bool grows = growsIn && build.canvas.arrival != null;
                entry.texture.SetPixels32(KitbashTextures.TexturePixels(build.canvas, false, grows));
                entry.texture.Apply(true, !grows);
                if (grows) KitbashGrowth.Start(new[] { entry.material }, GrowthTicks);
            },
            name);
        }

        /// <summary>
        /// The hardblood lance keeps its own drawn texture (no crystal rebuild), but still grows in: a copy of its
        /// texture with each pixel's arrival in its alpha, revealed from the butt to the tip as the blood lines run along
        /// it. A lance that's already there (loaded from a save) just draws as itself.
        /// </summary>
        public static void ApplyLance(ThingWithComps lance, bool growsIn)
        {
            ForgetEverythingIfNewGame();
            if (GraphicField == null || entriesByReplica.ContainsKey(lance.thingIDNumber)) return;
            var entry = new ReplicaEntry();
            entriesByReplica[lance.thingIDNumber] = entry;
            HardbloodLanceExtension settings = lance.def.GetModExtension<HardbloodLanceExtension>();
            entry.grip = settings.gripOnSprite;
            entry.tip = settings.tipOnSprite;

            Graphic original = lance.def.graphic;
            Texture originalTexture = original?.MatSingle?.mainTexture;
            if (!growsIn || originalTexture == null) return;

            int width = originalTexture.width;
            int height = originalTexture.height;
            Color32[] topFirst = KitbashTextures.ReadPixels(originalTexture, width, height, false);
            var bottomFirst = new Color32[topFirst.Length];
            Vector2 axis = settings.tipOnSprite - settings.gripOnSprite;
            float axisLengthSquared = Mathf.Max(0.0001f, axis.sqrMagnitude);
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Color32 pixel = topFirst[row * width + column];
                    Vector2 spritePoint = new Vector2(column / (float)Mathf.Max(1, width - 1) - 0.5f, 0.5f - row / (float)Mathf.Max(1, height - 1));
                    float arrival = Mathf.Clamp01(Vector2.Dot(spritePoint - settings.gripOnSprite, axis) / axisLengthSquared);   // 0 at the butt, 1 at the tip
                    pixel.a = pixel.a < 64 ? (byte)0 : KitbashGrowth.EncodeArrival(arrival);
                    bottomFirst[(height - 1 - row) * width + column] = pixel;
                }
            }

            string name = $"HardbloodLance_{lance.thingIDNumber}";
            entry.texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            entry.texture.SetPixels32(bottomFirst);
            entry.texture.Apply(true, false);
            entry.material = new Material(ShaderDatabase.Cutout)
            {
                name = name,
                mainTexture = entry.texture,
                color = Color.white
            };
            entry.graphic = HardbloodReplicaGraphic.Create(entry.material, original.drawSize, name);
            GraphicField.SetValue(lance, entry.graphic);
            KitbashGrowth.Start(new[] { entry.material }, Mathf.RoundToInt(settings.formSeconds * GenTicks.TicksPerRealSecond));
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
        /// A replica dissolved: its textures are freed a few frames later, since gizmos gathered this frame may still
        /// draw its icon.
        /// </summary>
        public static void Forget(Thing replica)
        {
            if (!entriesByReplica.TryGetValue(replica.thingIDNumber, out ReplicaEntry entry)) return;
            entriesByReplica.Remove(replica.thingIDNumber);
            if (entry.texture != null) KitbashGrowth.Stop(new[] { entry.texture });
            entry.retiredFrame = Time.frameCount;
            retired.Add(entry);
        }

        /// <summary>
        /// Frees the textures of replicas that dissolved a few frames ago. Called every frame.
        /// </summary>
        public static void FreeRetired()
        {
            for (int index = retired.Count - 1; index >= 0; index--)
            {
                if (Time.frameCount - retired[index].retiredFrame < RetireFrames) continue;
                Destroy(retired[index]);
                retired.RemoveAt(index);
            }
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
            foreach (ReplicaEntry entry in retired) Destroy(entry);
            entriesByReplica.Clear();
            retired.Clear();
        }
    }
}
