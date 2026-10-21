using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Aquaphobia
{
    /// <summary>
    /// "Afraid of water" moodlet. Stage 0 for weak exposure, stage 1 for strong.
    /// </summary>
    public class ThoughtWorker_Aquaphobia : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            Gene_Aquaphobia aquaphobiaGene = pawn.genes?.GetFirstGeneOfType<Gene_Aquaphobia>();
            if (aquaphobiaGene == null || !aquaphobiaGene.Active) return ThoughtState.Inactive;

            AquaphobiaExposure exposure = aquaphobiaGene.Exposure;
            if (exposure == AquaphobiaExposure.Strong) return ThoughtState.ActiveAtStage(1);
            if (exposure == AquaphobiaExposure.Weak) return ThoughtState.ActiveAtStage(0);
            return ThoughtState.Inactive;
        }
    }
}
