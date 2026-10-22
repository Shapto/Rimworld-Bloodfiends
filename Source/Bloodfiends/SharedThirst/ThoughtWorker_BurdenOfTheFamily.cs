using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.SharedThirst
{
    public class BurdenOfTheFamilyExtension : DefModExtension
    {
        public List<int> stageMaximumOtherMembers = new List<int>();
    }

    public class ThoughtWorker_BurdenOfTheFamily : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            Gene_SharedThirst sharedThirst = pawn.genes?.GetFirstGeneOfType<Gene_SharedThirst>();
            List<int> stageMaximums = def.GetModExtension<BurdenOfTheFamilyExtension>()?.stageMaximumOtherMembers;
            if (sharedThirst == null || !sharedThirst.Active || stageMaximums == null) return ThoughtState.Inactive;

            int otherMemberCount = sharedThirst.OtherFamilyMemberCount;
            for (int stageIndex = 0; stageIndex < stageMaximums.Count; stageIndex++)
            {
                if (otherMemberCount <= stageMaximums[stageIndex]) return ThoughtState.ActiveAtStage(stageIndex);
            }
            return ThoughtState.Inactive;
        }
    }
}
