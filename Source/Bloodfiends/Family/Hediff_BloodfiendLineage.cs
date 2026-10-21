using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Family
{
    public class Hediff_BloodfiendLineage : HediffWithComps
    {
        public int nodeId = -1;
        public BloodfiendFamilyNode Node => GameComponent_BloodfiendFamilies.Instance?.GetNode(nodeId);
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            SyncFromNode();
        }

        public void SyncFromNode()
        {
            BloodfiendFamilyNode node = Node;
            if (node != null) Severity = node.generationNumber;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nodeId, "nodeId", -1);
        }
    }
}
