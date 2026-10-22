using Bloodfiends.BloodCommand;
using Bloodfiends.Family;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hardblood
{
    public class GameComponent_HardbloodReplicas : GameComponent
    {
        private const int UpkeepIntervalTicks = 60;

        private List<Thing> replicas = new List<Thing>();
        private readonly HashSet<Thing> replicaSet = new HashSet<Thing>();
        private readonly Dictionary<Thing, Pawn> lastHolders = new Dictionary<Thing, Pawn>();   // so a dropped replica drains back into whoever dropped it

        public static GameComponent_HardbloodReplicas Instance => Current.Game?.GetComponent<GameComponent_HardbloodReplicas>();

        public GameComponent_HardbloodReplicas(Game game)
        {
        }

        public bool IsReplica(Thing thing) => thing != null && replicaSet.Contains(thing);

        /// <summary>
        /// Marks a new weapon as a replica and starts building its look. "growsIn": the crystal hardens in over a few
        /// seconds (a freshly manifested one) instead of just being there.
        /// </summary>
        public void Register(ThingWithComps replica, bool growsIn)
        {
            if (!replicaSet.Add(replica)) return;
            replicas.Add(replica);
            HardbloodReplicaGraphics.Apply(replica, growsIn);
        }

        /// <summary>
        /// The blood lines forming the replica in the holder's hands.
        /// </summary>
        public void StartForming(ThingWithComps replica, Pawn holder)
        {
            if (!holder.Spawned) return;
            WeaponFormingEffect.DefaultEnds(out Vector3 gripOffset, out Vector3 tipOffset);
            var effect = new WeaponFormingEffect(MapComponent_BloodCommand.BloodPalette, holder, replica, () => HardbloodReplicaGraphics.GripOf(replica), () => HardbloodReplicaGraphics.TipOf(replica), gripOffset, tipOffset, HardbloodWeaponry.Settings.lineWidth, false, replica.thingIDNumber, HardbloodWeaponry.Settings.formSeconds);
            MapComponent_BrushLines.Start(holder.Map, effect);
        }

        public override void GameComponentTick()
        {
            if (replicas.Count == 0) return;
            bool upkeepTick = Find.TickManager.TicksGame % UpkeepIntervalTicks == 0;
            for (int index = replicas.Count - 1; index >= 0; index--)
            {
                Thing replica = replicas[index];
                if (replica == null || replica.Destroyed)
                {
                    Forget(replica);
                    continue;
                }
                Pawn holder = (replica.ParentHolder as Pawn_EquipmentTracker)?.pawn;
                if (!StillHeld(replica, holder) || upkeepTick && !PayUpkeep(holder))
                {
                    Dissolve(replica);
                    continue;
                }
                lastHolders[replica] = holder;
            }
        }

        /// <summary>
        /// Saved games don't keep generated textures, so loaded replicas get their look back here (every frame, so it's
        /// there even while paused).
        /// </summary>
        public override void GameComponentUpdate()
        {
            foreach (Thing replica in replicas)
            {
                if (replica is ThingWithComps weapon && !weapon.Destroyed) HardbloodReplicaGraphics.EnsureApplied(weapon);
            }
        }

        private static bool StillHeld(Thing replica, Pawn holder)
        {
            if (holder == null || holder.Dead || holder.Downed || holder.equipment?.Primary != replica) return false;
            return holder.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active == true;
        }

        private static bool PayUpkeep(Pawn holder)
        {
            Gene_Hemogen hemogenGene = holder.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene == null) return false;
            float drain = HardbloodWeaponry.UpkeepPerDay(holder) * UpkeepIntervalTicks / GenDate.TicksPerDay;
            if (hemogenGene.Value <= drain) return false;
            GeneUtility.OffsetHemogen(holder, -drain);
            return true;
        }

        /// <summary>
        /// Breaks the replica back into blood lines that drain into its holder, and destroys it.
        /// </summary>
        public void Dissolve(Thing replica)
        {
            Pawn holder = (replica.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            if (holder == null) lastHolders.TryGetValue(replica, out holder);
            if (holder != null && holder.Spawned) StartDissolving(replica, holder);
            Forget(replica);
            if (!replica.Destroyed) replica.Destroy(DestroyMode.Vanish);
        }

        private void StartDissolving(Thing replica, Pawn holder)
        {
            Vector2 grip = HardbloodReplicaGraphics.GripOf(replica);
            Vector2 tip = HardbloodReplicaGraphics.TipOf(replica);
            if (!WeaponFormingEffect.TryGetWeaponEnds(holder, replica, grip, tip, out Vector3 gripOffset, out Vector3 tipOffset))
            {
                if (replica.Spawned)
                {
                    // dropped: lying on the ground
                    Vector3 fromHolder = replica.DrawPos - holder.DrawPos;
                    gripOffset = fromHolder + new Vector3(-0.3f, 0f, -0.3f);
                    tipOffset = fromHolder + new Vector3(0.35f, 0f, 0.35f);
                }
                else
                {
                    WeaponFormingEffect.DefaultEnds(out gripOffset, out tipOffset);
                }
            }
            var effect = new WeaponFormingEffect(MapComponent_BloodCommand.BloodPalette, holder, null, () => grip, () => tip, gripOffset, tipOffset, HardbloodWeaponry.Settings.lineWidth, true, replica.thingIDNumber, HardbloodWeaponry.Settings.formSeconds * 0.6f);
            MapComponent_BrushLines.Start(holder.Map, effect);
        }

        private void Forget(Thing replica)
        {
            replicas.Remove(replica);
            if (replica == null) return;
            replicaSet.Remove(replica);
            lastHolders.Remove(replica);
            HardbloodReplicaGraphics.Forget(replica);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref replicas, "replicas", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (replicas == null) replicas = new List<Thing>();
                replicas.RemoveAll(replica => replica == null);
                replicaSet.Clear();
                replicaSet.AddRange(replicas);
                lastHolders.Clear();
            }
        }
    }
}
