using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bloodfiends
{
    public static class BloodfiendsSounds
    {
        public static void PlayAt(SoundDef sound, Thing thing)
        {
            if (sound == null || thing == null || !thing.Spawned) return;
            sound.PlayOneShot(SoundInfo.InMap(thing));
        }

        public static void PlayAt(SoundDef sound, Vector3 position, Map map)
        {
            if (sound == null || map == null) return;
            IntVec3 cell = position.ToIntVec3().ClampInsideMap(map);
            sound.PlayOneShot(SoundInfo.InMap(new TargetInfo(cell, map)));
        }

        public static void PlayDissolve(Thing thing) => PlayAt(SoundDefOf.WastepackDissolution, thing);

        public static void PlayDissolve(Vector3 position, Map map) => PlayAt(SoundDefOf.WastepackDissolution, position, map);
        public static void PlayBloodRush(Vector3 position, Map map)
        {
            PlayAt(SoundDefOf.Psycast_Skip_Exit, position, map);
            PlayAt(SoundDefOf.Execute_Cut, position, map);
        }
    }
}