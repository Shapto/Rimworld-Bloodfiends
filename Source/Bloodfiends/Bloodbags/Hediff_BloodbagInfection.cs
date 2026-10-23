using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Bloodbags
{
    public class BloodbagInfectionExtension : DefModExtension
    {
        public float chancePerHit = 0.1f;
    }

    public class Hediff_BloodbagInfection : HediffWithComps
    {
        public int familyId = -1;
        public Faction faction;
        private bool queuedToTurn;   // not saved: after loading it's simply queued again

        public override void PostTick()
        {
            base.PostTick();
            if (queuedToTurn || Severity < def.maxSeverity || pawn.Dead) return;
            queuedToTurn = true;
            GameComponent_Bloodbags.Instance?.QueueTurning(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref familyId, "familyId", -1);
            Scribe_References.Look(ref faction, "faction");
        }
    }
}
