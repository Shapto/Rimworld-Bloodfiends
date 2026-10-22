using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.SharedThirst
{
    public class ThoughtWorker_FamilyBond : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn otherPawn)
        {
            if (pawn.genes?.GetFirstGeneOfType<Gene_SharedThirst>()?.Active != true) return ThoughtState.Inactive;
            return GameComponent_BloodfiendFamilies.Instance?.AreSameFamily(pawn, otherPawn) == true ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }
}
