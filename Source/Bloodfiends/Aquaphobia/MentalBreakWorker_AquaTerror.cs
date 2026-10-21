using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Bloodfiends.Aquaphobia
{
    /// <summary>
    /// Water terror can only start while the pawn is strongly exposed to water.
    /// </summary>
    public class MentalBreakWorker_AquaTerror : MentalBreakWorker
    {
        public override bool BreakCanOccur(Pawn pawn) => base.BreakCanOccur(pawn) && pawn.genes?.GetFirstGeneOfType<Gene_Aquaphobia>()?.Exposure == AquaphobiaExposure.Strong;
    }
}
