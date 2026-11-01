using RimWorld;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

public class QuestPart_DiscardUnarrivedPawns : QuestPart
{
    public List<Pawn> pawns = new List<Pawn>();

    public override void Cleanup()
    {
        base.Cleanup();
        if (quest.State == QuestState.EndedSuccess) return;

        foreach (Pawn pawn in pawns)
        {
            if (pawn == null || pawn.Dead || pawn.Discarded || pawn.SpawnedOrAnyParentSpawned || pawn.Faction == Faction.OfPlayer || !pawn.IsWorldPawn()) continue;
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && pawns == null) pawns = new List<Pawn>();
    }
}