using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bloodfiends.Aquaphobia
{
    public class MentalState_PanicFleeWater : MentalState
    {
        private int lastWaterSeenTick = -1;

        protected override bool CanEndBeforeMaxDurationNow => false;

        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (!pawn.IsHashIntervalTick(30, delta)) return;

            bool strongExposure = pawn.genes?.GetFirstGeneOfType<Gene_Aquaphobia>()?.Exposure == AquaphobiaExposure.Strong;
            if (lastWaterSeenTick < 0 || strongExposure) lastWaterSeenTick = Find.TickManager.TicksGame;

            if (Find.TickManager.TicksGame >= lastWaterSeenTick + def.minTicksBeforeRecovery) RecoverFromState();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastWaterSeenTick, "lastWaterSeenTick", -1);
        }
    }
}
