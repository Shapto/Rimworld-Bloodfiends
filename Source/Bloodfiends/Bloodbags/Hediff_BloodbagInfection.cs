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
        private bool queuedToTurn;
        public Pawn master;
        public override bool ShouldRemove => base.ShouldRemove || !BloodbagUtility.CanBecomeBloodbag(pawn) && !pawn.Dead;

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
            Scribe_References.Look(ref master, "master");
        }
    }
}
