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

        public const int BloodbagGeneration = 8;

        public static MutantDef BloodbagDef => ModsConfig.AnomalyActive ? DefDatabase<MutantDef>.GetNamedSilentFail(BloodbagMutantName) : null;

        public static bool IsBloodbag(Pawn pawn) => pawn != null && pawn.IsMutant && pawn.mutant.Def.defName == BloodbagMutantName;

        public static int FamilyOf(Pawn bloodbag) => bloodbag.health.hediffSet.GetFirstHediff<Hediff_HardbloodGrowths>()?.familyId ?? -1;

        public static Pawn MasterOf(Pawn bloodbag) => bloodbag?.health.hediffSet.GetFirstHediff<Hediff_HardbloodGrowths>()?.master;

        public static Pawn MasterFor(Pawn maker)
        {
            if (maker == null) return null;
            if (IsBloodbag(maker)) return MasterOf(maker);
            return GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(maker) != null ? maker : null;
        }

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
            ThingWithComps heldWeapon = pawn.equipment?.Primary;
            if (heldWeapon != null) pawn.equipment.Remove(heldWeapon);

            MutantUtility.SetPawnAsMutantInstantly(pawn, bloodbagDef);

            if (heldWeapon != null) pawn.equipment.AddEquipment(heldWeapon);

            Gene_Hemogen hemogenGene = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene != null)
            {
                hemogenGene.SetMax(hemogenGene.Max + (bloodbagDef.GetModExtension<BloodbagExtension>()?.hemogenMaxOffset ?? 2f));
                hemogenGene.Value = hemogenGene.Max;
            }

            Hediff_HardbloodGrowths growths = pawn.health.hediffSet.GetFirstHediff<Hediff_HardbloodGrowths>();
            if (growths != null)
            {
                growths.familyId = familyId;
                growths.master = MasterFor(instigator);
            }
            if (pawn.SpawnedOrAnyParentSpawned) FilthMaker.TryMakeFilth(pawn.PositionHeld, pawn.MapHeld, ThingDefOf.Filth_Blood, 3);
        }
    }
}
