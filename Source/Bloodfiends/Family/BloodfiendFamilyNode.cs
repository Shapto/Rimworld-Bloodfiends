using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class BloodfiendFamilyNode : IExposable
    {
        public int nodeId;
        public int familyId;
        public int generationNumber;
        public Pawn bloodfiendPawn;
        public int embracerNodeId = -1;
        public List<int> embracedNodeIds = new List<int>();
        public int kindredCreatedCount;

        public bool HasLivingPawn => bloodfiendPawn != null && !bloodfiendPawn.Dead;

        public void ExposeData()
        {
            Scribe_Values.Look(ref nodeId, "nodeId");
            Scribe_Values.Look(ref familyId, "familyId");
            Scribe_Values.Look(ref generationNumber, "generationNumber");
            Scribe_References.Look(ref bloodfiendPawn, "bloodfiendPawn");
            Scribe_Values.Look(ref embracerNodeId, "embracerNodeId", -1);
            Scribe_Collections.Look(ref embracedNodeIds, "embracedNodeIds", LookMode.Value);
            Scribe_Values.Look(ref kindredCreatedCount, "kindredCreatedCount");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && embracedNodeIds == null) embracedNodeIds = new List<int>();
        }
    }
}
