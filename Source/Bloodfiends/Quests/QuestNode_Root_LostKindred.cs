using Bloodfiends.Family;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using Verse;

namespace Bloodfiends.Quests
{
    public class QuestNode_Root_LostKindred : QuestNode_Root_WandererJoin
    {
        private const int TimeoutTicks = 60000;

        private string signalAccept;
        private string signalReject;

        protected override bool TestRunInt(Slate slate) => base.TestRunInt(slate) && GameComponent_BloodfiendFamilies.Instance?.CanPlaceLostKindred() == true;

        protected override void RunInt()
        {
            base.RunInt();
            Quest quest = QuestGen.quest;
            quest.Delay(TimeoutTicks, () => QuestGen_End.End(quest, QuestEndOutcome.Fail));
        }

        public override Pawn GeneratePawn() => GeneratePawn_NewTemp(null);

        public override Pawn GeneratePawn_NewTemp(Map map)
        {
            var request = new PawnGenerationRequest(PawnKindDefOf.Villager, null, PawnGenerationContext.NonPlayer, map?.Tile, forceGenerateNewPawn: true, canGeneratePawnRelations: false, forcedXenotype: BloodfiendsDefOf.Bloodfiends_Bloodfiend, forceRecruitable: true);
            Pawn pawn;
            GameComponent_BloodfiendFamilies.placingLostKindred = true;
            try
            {
                pawn = PawnGenerator.GeneratePawn(request);
            }
            finally
            {
                GameComponent_BloodfiendFamilies.placingLostKindred = false;
            }
            if (!pawn.IsWorldPawn()) Find.WorldPawns.PassToWorld(pawn);
            return pawn;
        }

        protected override void AddSpawnPawnQuestParts(Quest quest, Map map, Pawn pawn)
        {
            signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            signalReject = QuestGenUtility.HardcodedSignalWithQuestID("Reject");
            quest.Signal(signalAccept, () =>
            {
                quest.SetFaction(Gen.YieldSingle(pawn), Faction.OfPlayer);
                quest.PawnsArrive(Gen.YieldSingle(pawn), null, map.Parent);
                QuestGen_End.End(quest, QuestEndOutcome.Success);
            });
            quest.Signal(signalReject, () =>
            {
                quest.GiveDiedOrDownedThoughts(pawn, PawnDiedOrDownedThoughtsKind.DeniedJoining);
                QuestGen_End.End(quest, QuestEndOutcome.Fail);
            });
        }

        [Obsolete]
        public override void SendLetter(Quest quest, Pawn pawn) => SendLetter_NewTemp(quest, pawn, Find.AnyPlayerHomeMap);

        public override void SendLetter_NewTemp(Quest quest, Pawn pawn, Map map)
        {
            GameComponent_BloodfiendFamilies families = GameComponent_BloodfiendFamilies.Instance;
            BloodfiendFamilyNode node = families?.GetNodeForPawn(pawn);
            Pawn relative = node == null ? null : families.PlayerRelativeIn(node.familyId);
            string tier = pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.CurStage?.label ?? "";

            TaggedString title = "Bloodfiends_LetterLabelLostKindred".Translate(pawn.Named("PAWN")).AdjustedFor(pawn);
            TaggedString letterText = relative != null ? "Bloodfiends_LetterLostKindred".Translate(pawn.Named("PAWN"), relative.Named("RELATIVE"), tier.Named("TIER")).AdjustedFor(pawn) : "LetterWandererJoins".Translate(pawn.Named("PAWN")).AdjustedFor(pawn);
            QuestNode_Root_WandererJoin_WalkIn.AppendCharityInfoToLetter("JoinerCharityInfo".Translate(pawn), ref letterText);
            QuestNode_Root_WandererJoin_WalkIn.ApplyBestSkillInfoToLetter(ref letterText, pawn);

            var letter = (ChoiceLetter_AcceptJoiner)LetterMaker.MakeLetter(title, letterText, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.overrideMap = map;
            letter.StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }
}