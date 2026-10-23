using Bloodfiends.Family;
using Bloodfiends.Hardblood;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Bloodbags
{
    public static class BloodbagUtility
    {
        public const string BloodbagMutantName = "Bloodfiends_Bloodbag";

        public static MutantDef BloodbagDef => ModsConfig.AnomalyActive ? DefDatabase<MutantDef>.GetNamedSilentFail(BloodbagMutantName) : null;

        public static bool IsBloodbag(Pawn pawn) => pawn != null && pawn.IsMutant && pawn.mutant.Def.defName == BloodbagMutantName;

        public static int FamilyOf(Pawn bloodbag) => bloodbag.health.hediffSet.GetFirstHediff<Hediff_HardbloodGrowths>()?.familyId ?? -1;

        public static bool CanBecomeBloodbag(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike || pawn.IsMutant) return false;
            return pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>() == null;
        }

        public static void MakeBloodbag(Pawn pawn, Faction faction, Pawn instigator, int familyId)
        {
            MutantDef bloodbagDef = BloodbagDef;
            if (bloodbagDef == null) return;

            if (faction != null && pawn.Faction != faction) RecruitUtility.Recruit(pawn, faction, instigator);
            pawn.genes?.SetXenotype(BloodfiendsDefOf.Bloodfiends_Bloodbag);
            MutantUtility.SetPawnAsMutantInstantly(pawn, bloodbagDef);

            Hediff_HardbloodGrowths growths = pawn.health.hediffSet.GetFirstHediff<Hediff_HardbloodGrowths>();
            if (growths != null) growths.familyId = familyId;
            if (pawn.SpawnedOrAnyParentSpawned) FilthMaker.TryMakeFilth(pawn.PositionHeld, pawn.MapHeld, ThingDefOf.Filth_Blood, 3);
        }
    }
}
