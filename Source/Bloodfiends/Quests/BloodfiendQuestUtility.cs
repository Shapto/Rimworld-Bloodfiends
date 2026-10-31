using System.Collections.Generic;
using System.Linq;
using Bloodfiends.Bloodbags;
using Bloodfiends.Family;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace Bloodfiends.Quests
{
    public static class BloodfiendQuestUtility
    {
        public static Faction MakeHostileTemporaryFaction()
        {
            List<FactionRelation> relations = Find.FactionManager.AllFactionsListForReading
                .Select(faction => new FactionRelation(faction, FactionRelationKind.Hostile))
                .ToList();
            Faction hostileFaction = FactionGenerator.NewGeneratedFactionWithRelations(FactionDefOf.Sanguophages, relations, hidden: true);
            hostileFaction.temporary = true;
            Find.FactionManager.Add(hostileFaction);
            return hostileFaction;
        }

        public static Pawn GenerateBloodfiend(PawnKindDef pawnKind, Faction faction, PlanetTile tile, bool forceRecruitable = false, int claimingNodeId = -1, int foundingGeneration = 0, int embracingNodeId = -1)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(pawnKind, faction, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true, forcedXenotype: BloodfiendsDefOf.Bloodfiends_Bloodfiend, forceRecruitable: forceRecruitable);

            GameComponent_BloodfiendFamilies.claimingNodeId = claimingNodeId;
            GameComponent_BloodfiendFamilies.foundingGeneration = foundingGeneration;
            GameComponent_BloodfiendFamilies.embracingNodeId = embracingNodeId;
            try
            {
                Pawn bloodfiend = PawnGenerator.GeneratePawn(request);
                QuestGen.AddToGeneratedPawns(bloodfiend);
                return bloodfiend;
            }
            finally
            {
                GameComponent_BloodfiendFamilies.claimingNodeId = -1;
                GameComponent_BloodfiendFamilies.foundingGeneration = 0;
                GameComponent_BloodfiendFamilies.embracingNodeId = -1;
            }
        }

        public static List<Pawn> GenerateGuards(Faction faction, PlanetTile tile, Pawn master, float guardPoints, int maximumGuardCount)
        {
            PawnKindDef guardKind = PawnKindDefOf.SanguophageThrall;
            int guardCount = Mathf.Clamp(Mathf.FloorToInt(guardPoints / guardKind.combatPower), 1, maximumGuardCount);
            int familyId = GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(master)?.familyId ?? -1;

            List<Pawn> guards = new List<Pawn>();
            for (int guardIndex = 0; guardIndex < guardCount; guardIndex++)
            {
                Pawn guard = PawnGenerator.GeneratePawn(new PawnGenerationRequest(guardKind, faction, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
                if (BloodbagUtility.BloodbagDef != null) BloodbagUtility.MakeBloodbag(guard, faction, master, familyId);
                QuestGen.AddToGeneratedPawns(guard);
                guards.Add(guard);
            }
            return guards;
        }

        public static string TierLabelOf(Pawn pawn) => pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.CurStage?.label ?? "";
    }
}