using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Bloodbags
{
    public class GameComponent_Bloodbags : GameComponent
    {
        private readonly List<Pawn> pendingTurns = new List<Pawn>();

        public static GameComponent_Bloodbags Instance => Current.Game?.GetComponent<GameComponent_Bloodbags>();

        public GameComponent_Bloodbags(Game game)
        {
        }

        public void QueueTurning(Pawn pawn)
        {
            if (!pendingTurns.Contains(pawn)) pendingTurns.Add(pawn);
        }

        private const int MasterlessCheckIntervalTicks = 250;

        public override void GameComponentTick()
        {
            TurnPendingInfections();
            if (Find.TickManager.TicksGame % MasterlessCheckIntervalTicks == 0) RampageMasterless();
        }

        private void TurnPendingInfections()
        {
            if (pendingTurns.Count == 0) return;
            foreach (Pawn pawn in pendingTurns)
            {
                Hediff_BloodbagInfection infection = pawn.health.hediffSet.GetFirstHediff<Hediff_BloodbagInfection>();
                if (infection == null || pawn.Dead || !BloodbagUtility.CanBecomeBloodbag(pawn)) continue;

                pawn.health.RemoveHediff(infection);
                BloodbagUtility.MakeBloodbag(pawn, infection.faction, null, infection.familyId);
                if (pawn.Faction == Faction.OfPlayer) Messages.Message("Bloodfiends_TurnedIntoBloodbag".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
            }
            pendingTurns.Clear();
        }
        private static void RampageMasterless()
        {
            GameComponent_BloodfiendFamilies families = GameComponent_BloodfiendFamilies.Instance;
            if (families == null) return;
            foreach (Pawn pawn in PawnsFinder.AllMaps_Spawned)
            {
                if (!BloodbagUtility.IsBloodbag(pawn) || pawn.Dead || pawn.Downed || pawn.InMentalState) continue;
                int familyId = BloodbagUtility.FamilyOf(pawn);
                if (familyId < 0 || families.FamilyHasLivingBloodfiend(familyId)) continue;
                pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "Bloodfiends_BloodbagMasterless".Translate(), forced: true, forceWake: true);
            }
        }
    }
}
