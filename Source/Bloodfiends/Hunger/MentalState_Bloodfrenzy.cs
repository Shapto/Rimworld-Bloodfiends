using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Bloodfiends.Hunger
{
    public class MentalState_Bloodfrenzy : MentalState_Berserk
    {
        protected override bool CanEndBeforeMaxDurationNow => false;

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (!pawn.IsHashIntervalTick(60, delta)) return;
            Gene_Hemogen hemogenGene = pawn.genes?.GetFirstGeneOfType<RimWorld.Gene_Hemogen>();
            float endsAbove = pawn.genes?.GetFirstGeneOfType<Gene_Withering>()?.Settings.frenzyEndsAbove ?? 0.25f;
            if (hemogenGene == null || hemogenGene.Value >= hemogenGene.Max * endsAbove) RecoverFromState();
        }
    }
}
