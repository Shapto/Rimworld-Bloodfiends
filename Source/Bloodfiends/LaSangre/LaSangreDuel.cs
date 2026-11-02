using System.Collections.Generic;
using System.Linq;
using Bloodfiends.BloodCommand;
using Bloodfiends.Family;
using Bloodfiends.Hardblood;
using HarmonyLib;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bloodfiends.LaSangre
{
    /// <summary>
    /// The La Sangre duel. When La Sangre would strike a Bloodfiend holding a formed lance, the best melee fighter among them
    /// pours all their hemogen into it, morphs it into Enhanced La Sangre and meets the dive mid-air. The lances lock tip to tip
    /// and grind through a few surges; then the winner breaks through, runs the loser through and swings them down into the ground on the lance. The blast never lands.
    /// </summary>
    public partial class LaSangreFlight
    {
        private const int DashTicks = 16;
        private const int FirstClashTick = DiveTick + DashTicks;
        private const int ClashIntervalTicks = 55;
        private const int ShoveDelayTicks = 12;
        private const int GrindSparkTicks = 5;
        private const int BreakHitstopTicks = 14;
        private const int SnapTicks = 6;
        private const int TableauTicks = 60;
        private const int WindUpTicks = 24;
        private const int SlamTicks = 5;
        private const int PinTicks = 90;
        private const int LandingTicks = 20;
        private const int LesserGeneration = 8;
        private const float DuelHeightFactor = 0.9f;
        private const float ReachShare = 0.97f;
        private const float DriftPerClash = 0.3f;
        private const float JitterDistance = 0.08f;
        private const float LungeShare = 1.4f;
        private const float ImpaleShare = 0.6f;
        private const float LiftHeight = 0.7f;
        private const float WindUpTilt = 1f;
        private const float SlamForwardShare = 0.5f;
        private const float SlamAheadDistance = 2.5f;
        private const float StandOffShare = 0.6f;
        private const float ClashFlashSize = 3f;
        private const float ClashShake = 1.5f;
        private const float FirstImpactFlashSize = 5f;
        private const float FirstImpactShake = 3f;
        private const float ImpaleShake = 3f;
        private const float SlamFlashSize = 6f;
        private const float SlamFilthRadius = 2.2f;

        private Pawn duelCaster;
        private Pawn duelist;
        private Vector3 duelistGround;
        private Vector3 meetPoint;
        private float duelistStrength = 1f;
        private bool duelistWasDrafted;
        private int casterClashWins;
        private int duelistClashWins;
        private int casterClashMask;
        private bool duelResolved;
        private bool casterWonDuel;

        private LanceLook duelistLanceLook;
        private LanceLook enhancedLanceLook;

        private int ResolveTick => FirstClashTick + Props.duelClashes * ClashIntervalTicks;

        private int BreakTick => ResolveTick + BreakHitstopTicks;

        private int ImpaledTick => BreakTick + SnapTicks;

        private int TableauEndTick => ImpaledTick + TableauTicks;

        private int SlamStartTick => TableauEndTick + WindUpTicks;

        private int SlamTick => SlamStartTick + SlamTicks;

        private int LandStartTick => SlamTick + PinTicks;

        private int LandTick => LandStartTick + LandingTicks;

        private float DuelHeight => Props.airHeight * DuelHeightFactor;

        private float TipDistance => (1f - Props.lanceGripFraction) * Props.lanceLength;

        private float Reach => TipDistance * ReachShare;

        private float ImpaleLength => TipDistance * ImpaleShare;

        private Vector3 WinnerFacing => casterWonDuel ? diveDirection : -diveDirection;

        /// <summary>
        /// The winner's sideways heading on screen, which the slam follows. Zero when the dive runs straight up or down.
        /// </summary>
        private Vector3 WinnerAhead => Mathf.Abs(WinnerFacing.x) > 0.1f ? new Vector3(Mathf.Sign(WinnerFacing.x), 0f, 0f) : Vector3.zero;

        private Vector3 WinnerStrike => Contact(Props.duelClashes) - WinnerFacing * Reach * (1f - LungeShare);

        private Vector3 ImpaledSpot => WinnerStrike + WinnerFacing * ImpaleLength;

        private Vector3 WindUpDirection => (WinnerFacing + Vector3.forward * WindUpTilt).normalized;

        private Vector3 SlamDirection => (WinnerAhead * SlamForwardShare + Vector3.back).normalized;

        /// <summary>
        /// Where the loser hits the ground: straight under where they were run through when the caster wins,
        /// a little further along the interceptor's thrust when the interceptor wins.
        /// </summary>
        private Vector3 SlamPoint => (ImpaledSpot - new Vector3(0f, 0f, DuelHeight) + WinnerAhead * (casterWonDuel ? 0f : SlamAheadDistance)).ToIntVec3().ToVector3Shifted();

        private Vector3 WinnerSlamSpot => SlamPoint - SlamDirection * ImpaleLength;

        private float WinnerSlamHeight => -SlamDirection.z * ImpaleLength;

        private Vector3 WinnerLanding => SlamPoint - (WinnerAhead == Vector3.zero ? WinnerFacing : WinnerAhead) * TipDistance * StandOffShare;

        private Pawn DuelWinner => casterWonDuel ? duelCaster : duelist;

        private Pawn DuelLoser => casterWonDuel ? duelist : duelCaster;

        internal bool DuelInvolves(Pawn pawn) => duelist != null && pawn != null && (pawn == duelist || pawn == duelCaster);

        internal Thing DuelistLance => duelist?.equipment?.Primary;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (duelist != null) LaSangreDuels.Register(this);
        }

        /// <summary>
        /// Looks for a Bloodfiend in the blast with a formed lance who would be hurt by it, and if there is one, starts the duel.
        /// </summary>
        private void TryStartDuel(Map map)
        {
            Pawn caster = FlyingPawn;
            if (caster == null) return;
            Pawn interceptor = FindInterceptor(caster, map);
            if (interceptor == null) return;

            Gene_Hemogen hemogenGene = interceptor.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            float spentFraction = hemogenGene != null && hemogenGene.Max > 0f ? Mathf.Clamp01(hemogenGene.Value / hemogenGene.Max) : 0f;
            if (hemogenGene != null) hemogenGene.Value = 0f;
            duelistStrength = Mathf.Max(1f, Props.maximumStrength * spentFraction);

            duelCaster = caster;
            duelist = interceptor;
            duelistGround = interceptor.DrawPos;
            duelistWasDrafted = interceptor.Drafted;
            Vector3 airGround = airPosition - new Vector3(0f, 0f, Props.airHeight);
            meetPoint = Vector3.Lerp(airGround, impactPosition, 0.5f) + new Vector3(0f, 0f, DuelHeight);

            interceptor.DeSpawn(DestroyMode.WillReplace);
            innerContainer.TryAdd(interceptor);
            LaSangreDuels.Register(this);

            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelEnhance, duelistGround, map);
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, duelistGround + new Vector3(0f, 0f, 0.5f), 16, new FloatRange(1f, 1.8f), new FloatRange(0.16f, 0.26f), seed + 29, 0.6f));
            Messages.Message("Bloodfiends_LaSangreIntercepted".Translate(interceptor.Named("DUELIST"), caster.Named("CASTER")), new LookTargets(this), MessageTypeDefOf.NeutralEvent);
        }

        private Pawn FindInterceptor(Pawn caster, Map map)
        {
            bool hitsAllies = BloodfiendsMod.Settings.laSangreHitsAllies;
            return GenRadial.RadialDistinctThingsAround(impactPosition.ToIntVec3(), map, Props.impactRadius, true)
                .OfType<Pawn>()
                .Where(pawn => pawn != caster && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.stances?.stunner?.Stunned != true && (hitsAllies || pawn.HostileTo(caster)))
                .Where(pawn => pawn.genes?.GetFirstGeneOfType<Gene_Bloodfiend>()?.Active == true && pawn.equipment?.Primary?.def == BloodfiendsDefOf.Bloodfiends_HardbloodLance)
                .OrderByDescending(pawn => pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0)
                .FirstOrDefault();
        }

        private void TickDuel(Map map)
        {
            FaceOpponents();
            TickSpectacle(map);
            if (ageTicks == DiveTick)
            {
                BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_SangreDive, airPosition, map);
                BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_SangreDive, duelistGround, map);
            }

            int ticksSinceFirstClash = ageTicks - FirstClashTick;
            int clashIndex = ticksSinceFirstClash / ClashIntervalTicks;
            if (ticksSinceFirstClash >= 0 && ticksSinceFirstClash % ClashIntervalTicks == 0 && clashIndex < Props.duelClashes) Clash(map, clashIndex);
            else if (ticksSinceFirstClash > 0 && ageTicks < ResolveTick && ticksSinceFirstClash % GrindSparkTicks == 0) GrindSparks(map, ticksSinceFirstClash);
            if (ageTicks == ResolveTick)
            {
                duelResolved = true;
                casterWonDuel = casterClashWins > duelistClashWins;
            }
            if (ageTicks == BreakTick) Impale(map);
            if (ageTicks == SlamTick) Slam(map);
            if (ageTicks >= LandTick) LandDuel(map);
        }

        /// <summary>
        /// One surge in the lock. Who takes it is a roll weighted by each side's power; the point where the lance tips meet
        /// is then shoved toward whoever lost it. The first one is the moment the two charges collide.
        /// </summary>
        private void Clash(Map map, int clashIndex)
        {
            float casterPower = DuelPower(duelCaster, strength);
            float duelistPower = DuelPower(duelist, duelistStrength);
            if (Rand.Chance(casterPower / (casterPower + duelistPower)))
            {
                casterClashWins++;
                casterClashMask |= 1 << clashIndex;
            }
            else duelistClashWins++;

            Vector3 contact = Contact(clashIndex);
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, contact, 18, new FloatRange(0.8f, 1.6f), new FloatRange(0.16f, 0.26f), seed + 23 + clashIndex, 0.5f));
            bool firstImpact = clashIndex == 0;
            FleckMaker.Static(contact, map, firstImpact ? FleckDefOf.ExplosionFlash : FleckDefOf.ShotFlash, firstImpact ? FirstImpactFlashSize : ClashFlashSize);
            if (!firstImpact) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelSurge, contact, map);
            Shake(map, firstImpact ? FirstImpactShake : ClashShake);
            SurgeSpectacle(map, contact, firstImpact);
        }

        /// <summary>
        /// Between surges the tips keep grinding against each other, throwing a little blood from the contact point.
        /// </summary>
        private void GrindSparks(Map map, int ticksSinceFirstClash)
        {
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, LockContact(), 6, new FloatRange(0.4f, 0.9f), new FloatRange(0.1f, 0.18f), seed + 101 + ticksSinceFirstClash, 0.3f));
            FleckMaker.ThrowMicroSparks(LockContact(), map);
        }

        /// <summary>
        /// Blood poured into the lance, sharpened by melee skill, with a little extra for purer blood.
        /// </summary>
        private static float DuelPower(Pawn pawn, float duelStrength)
        {
            if (pawn == null) return 0.01f;
            int meleeLevel = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            int generation = GameComponent_BloodfiendFamilies.Instance?.GetNodeForPawn(pawn)?.generationNumber ?? LesserGeneration;
            return duelStrength * (1f + meleeLevel / 20f) * (1f + Mathf.Max(0, LesserGeneration - generation) * 0.05f);
        }

        /// <summary>
        /// The breakthrough, after a moment frozen in the flash: the loser's lance shatters at the tips
        /// and the winner charges through, running them through.
        /// </summary>
        private void Impale(Map map)
        {
            Vector3 contact = Contact(Props.duelClashes);
            Vector3 loserPosition = ImpaledSpot;
            Vector3 loserFacing = -WinnerFacing;
            MapComponent_BrushLines.Start(map, new ShardLineEffect(MapComponent_BloodCommand.BloodPalette, contact, contact - loserFacing * Reach, DuelHeight, 18, seed + 31, 1.1f));
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, loserPosition, 24, new FloatRange(1f, 2f), new FloatRange(0.18f, 0.3f), seed + 37, 0.6f));
            FleckMaker.Static(contact, map, FleckDefOf.ExplosionFlash, FirstImpactFlashSize);
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodShatter, loserPosition, map);
            Shake(map, ImpaleShake);
            if (casterWonDuel && DuelistLance != null) GameComponent_HardbloodReplicas.Instance?.Shatter(DuelistLance);
        }

        /// <summary>
        /// The loser hits the ground: La Sangre's own impact, a crater of blood, and the brunt of the damage.
        /// </summary>
        private void Slam(Map map)
        {
            Vector3 slamPoint = SlamPoint;
            Pawn loser = DuelLoser;
            Pawn winner = DuelWinner;
            PutDownDuelPawn(loser, slamPoint, -SlamDirection);

            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, slamPoint, 36, new FloatRange(1.5f, 3f), new FloatRange(0.22f, 0.38f), seed + 41, 0.8f));
            FleckMaker.Static(slamPoint, map, FleckDefOf.ExplosionFlash, SlamFlashSize);
            Props.impactSound?.PlayOneShot(new TargetInfo(slamPoint.ToIntVec3(), map));
            Shake(map, Props.cameraShake);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(slamPoint.ToIntVec3(), SlamFilthRadius, true))
                if (cell.InBounds(map) && Rand.Chance(0.6f)) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood, Rand.RangeInclusive(1, 3));

            float winnerStrength = casterWonDuel ? strength : duelistStrength;
            Strike(loser, winner, Props.damage * winnerStrength * Props.duelLoserDamageFactor, stun: true);
        }

        private void LandDuel(Map map)
        {
            Pawn winner = DuelWinner;
            Pawn loser = DuelLoser;
            PutDownDuelPawn(winner, WinnerLanding, WinnerFacing);
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, WinnerLanding, 14, new FloatRange(0.8f, 1.5f), new FloatRange(0.12f, 0.2f), seed + 43, 0.5f));
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_DuelLanding, WinnerLanding, map);

            float loserStrength = casterWonDuel ? duelistStrength : strength;
            Strike(winner, loser, Props.damage * loserStrength * Props.duelWinnerDamageFactor, stun: false);
            if (winner != null && loser != null) Messages.Message("Bloodfiends_LaSangreDuelWon".Translate(winner.Named("WINNER"), loser.Named("LOSER")), new LookTargets(winner), MessageTypeDefOf.NeutralEvent);

            LaSangreDuels.Unregister(this);
            duelist = null;
            duelCaster = null;
            Destroy();
        }

        private void Strike(Pawn victim, Pawn attacker, float damage, bool stun)
        {
            if (victim == null || victim.Dead || !victim.Spawned) return;
            victim.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Stab, damage, Props.armorPenetration, -1f, attacker));
            if (stun && !victim.Dead) victim.stances?.stunner?.StunFor(Props.stunTicks, attacker);
        }

        private void PutDownDuelPawn(Pawn pawn, Vector3 spot, Vector3 facing)
        {
            if (pawn == null || !innerContainer.Contains(pawn)) return;
            Map map = Map;
            IntVec3 cell = spot.ToIntVec3();
            if (!cell.InBounds(map) || !cell.Standable(map)) cell = CellFinder.StandableCellNear(cell, map, 4f);
            if (!cell.IsValid) cell = Position;
            innerContainer.Remove(pawn);
            GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
            pawn.Rotation = FacingToward(facing);
            bool draftAgain = pawn == duelist ? duelistWasDrafted : wasDrafted;
            if (pawn.drafter != null && draftAgain) pawn.drafter.Drafted = true;
        }

        /// <summary>
        /// If the flight is destroyed mid-duel (the map closing, say), both duelists are put down where they are.
        /// </summary>
        private void EndDuelEarly()
        {
            if (Spawned)
            {
                PutDownDuelPawn(duelist, DuelPlace(false).position, -diveDirection);
                PutDownDuelPawn(duelCaster, DuelPlace(true).position, diveDirection);
            }
            LaSangreDuels.Unregister(this);
            duelist = null;
            duelCaster = null;
        }

        private void FaceOpponents()
        {
            Vector3 casterPosition = DuelPlace(true).position;
            Vector3 duelistPosition = DuelPlace(false).position;
            if (duelCaster != null && innerContainer.Contains(duelCaster)) duelCaster.Rotation = FacingToward(duelistPosition - casterPosition);
            if (duelist != null && innerContainer.Contains(duelist)) duelist.Rotation = FacingToward(casterPosition - duelistPosition);
        }

        private static Rot4 FacingToward(Vector3 offset) => Rot4.FromAngleFlat(new Vector3(offset.x, 0f, offset.z).AngleFlat());

        private static void Shake(Map map, float strength)
        {
            if (map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(strength);
        }

        /// <summary>
        /// Where the lances meet after the first few clashes: shoved toward whoever has lost more of them.
        /// </summary>
        private Vector3 Contact(int clashesCounted)
        {
            int winDifference = 0;
            for (int clashIndex = 0; clashIndex < clashesCounted && clashIndex < Props.duelClashes; clashIndex++) winDifference += CasterTookClash(clashIndex) ? 1 : -1;
            return meetPoint + diveDirection * winDifference * DriftPerClash;
        }

        private bool CasterTookClash(int clashIndex) => (casterClashMask & (1 << clashIndex)) != 0;

        private Vector3 Jitter(float pawnSeed)
        {
            float x = Mathf.PerlinNoise(ageTicks * 0.6f, pawnSeed) - 0.5f;
            float z = Mathf.PerlinNoise(pawnSeed, ageTicks * 0.6f) - 0.5f;
            return new Vector3(x, 0f, z) * 2f * JitterDistance;
        }

        /// <summary>
        /// Where a duelist is drawn and how high off the ground (for the shadow): the dash, the lock,
        /// then the winner's breakthrough and the loser carried along on their lance.
        /// </summary>
        private (Vector3 position, float height) DuelPlace(bool forCaster)
        {
            Vector3 facing = forCaster ? diveDirection : -diveDirection;
            Vector3 start = forCaster ? airPosition : duelistGround;
            float startHeight = forCaster ? Props.airHeight : 0f;
            if (ageTicks < DiveTick) return (start, startHeight);
            if (ageTicks < FirstClashTick)
            {
                float dash = EaseIn(DiveTick, FirstClashTick);
                return (Vector3.Lerp(start, Contact(0) - facing * Reach, dash), Mathf.Lerp(startHeight, DuelHeight, dash));
            }
            if (ageTicks < ResolveTick) return (LockPlace(forCaster, facing), DuelHeight);
            return forCaster == casterWonDuel ? WinnerPlace() : LoserPlace();
        }

        /// <summary>
        /// Where the tips meet during the lock: each surge holds for a moment, then the contact point is shoved toward whoever lost it.
        /// </summary>
        private Vector3 LockContact()
        {
            int ticksIntoLock = Mathf.Max(0, ageTicks - FirstClashTick);
            int clashIndex = Mathf.Min(ticksIntoLock / ClashIntervalTicks, Props.duelClashes - 1);
            int surgeTick = FirstClashTick + clashIndex * ClashIntervalTicks;
            return Vector3.Lerp(Contact(clashIndex), Contact(clashIndex + 1), EaseOut(surgeTick + ShoveDelayTicks, surgeTick + ClashIntervalTicks));
        }

        /// <summary>
        /// Both pressing tip to tip along one line, shuddering against each other.
        /// </summary>
        private Vector3 LockPlace(bool forCaster, Vector3 facing) => LockContact() - facing * Reach + Jitter(forCaster ? 1.7f : 4.3f);

        /// <summary>
        /// The winner charges through, hangs there for a moment with the loser run through, lifts them on the lance,
        /// swings them down into the ground, holds them pinned there, then drops down beside the crater.
        /// </summary>
        private (Vector3 position, float height) WinnerPlace()
        {
            Vector3 locked = Contact(Props.duelClashes) - WinnerFacing * Reach;
            Vector3 lifted = WinnerStrike + Vector3.forward * LiftHeight;
            float liftedHeight = DuelHeight + LiftHeight;
            if (ageTicks < ImpaledTick) return (Vector3.Lerp(locked, WinnerStrike, EaseOut(BreakTick, ImpaledTick)), DuelHeight);
            if (ageTicks < TableauEndTick) return (WinnerStrike, DuelHeight);
            if (ageTicks < SlamStartTick)
            {
                float lift = EaseOut(TableauEndTick, SlamStartTick);
                return (Vector3.Lerp(WinnerStrike, lifted, lift), Mathf.Lerp(DuelHeight, liftedHeight, lift));
            }
            if (ageTicks < SlamTick)
            {
                float slam = EaseIn(SlamStartTick, SlamTick);
                return (Vector3.Lerp(lifted, WinnerSlamSpot, slam), Mathf.Lerp(liftedHeight, WinnerSlamHeight, slam));
            }
            if (ageTicks < LandStartTick) return (WinnerSlamSpot + Jitter(7.1f) * 0.5f, WinnerSlamHeight);
            float drop = BrushStroke.SmoothStep(LandStartTick, LandTick, ageTicks);
            return (Vector3.Lerp(WinnerSlamSpot, WinnerLanding, drop), Mathf.Lerp(WinnerSlamHeight, 0f, drop));
        }

        /// <summary>
        /// The loser is knocked onto the winner's lance, then stays stuck on it wherever the lance points, all the way into the ground.
        /// </summary>
        private (Vector3 position, float height) LoserPlace()
        {
            Vector3 locked = Contact(Props.duelClashes) + WinnerFacing * Reach;
            if (ageTicks < ImpaledTick) return (Vector3.Lerp(locked, ImpaledSpot, EaseOut(BreakTick, ImpaledTick)), DuelHeight);
            if (ageTicks >= SlamTick) return (SlamPoint, 0f);

            Vector3 onLance = WinnerPlace().position + ImpaleDirection() * ImpaleLength;
            if (ageTicks < TableauEndTick) return (onLance + Jitter(9.3f) * 0.4f, DuelHeight);
            if (ageTicks < SlamStartTick) return (onLance + Jitter(9.3f), Mathf.Lerp(DuelHeight, DuelHeight + LiftHeight, EaseOut(TableauEndTick, SlamStartTick)));
            return (onLance, Mathf.Lerp(DuelHeight + LiftHeight, 0f, EaseIn(SlamStartTick, SlamTick)));
        }

        /// <summary>
        /// Where the winner's lance points once the loser is on it: still along the charge, tipped up as they lift, then swung down into the ground.
        /// </summary>
        private Vector3 ImpaleDirection()
        {
            if (ageTicks < TableauEndTick) return WinnerFacing;
            if (ageTicks < SlamStartTick) return Vector3.Slerp(WinnerFacing, WindUpDirection, EaseOut(TableauEndTick, SlamStartTick));
            if (ageTicks < SlamTick) return Vector3.Slerp(WindUpDirection, SlamDirection, EaseIn(SlamStartTick, SlamTick));
            return SlamDirection;
        }

        private float EaseIn(int fromTick, int toTick) => Mathf.Pow(Mathf.InverseLerp(fromTick, toTick, ageTicks), 2f);

        private float EaseOut(int fromTick, int toTick) => 1f - Mathf.Pow(1f - Mathf.InverseLerp(fromTick, toTick, ageTicks), 2f);

        private void DrawDuelPawns(DrawPhase phase, bool flip)
        {
            DrawDuelPawn(duelCaster, true, phase, flip);
            DrawDuelPawn(duelist, false, phase, flip);
        }

        private void DrawDuelPawn(Pawn pawn, bool isCaster, DrawPhase phase, bool flip)
        {
            if (pawn == null || !innerContainer.Contains(pawn)) return;
            if (isCaster && ageTicks < DiveTick && !PawnVisible) return;
            (Vector3 position, float height) = DuelPlace(isCaster);
            float altitude = height > 0.05f ? AltitudeLayer.Skyfaller.AltitudeFor() : AltitudeLayer.Pawn.AltitudeFor();
            pawn.DynamicDrawPhaseAt(phase, position.WithY(altitude), flip);
        }

        private void DrawDuel(float airAltitude)
        {
            EnsureDuelLooks();
            (Vector3 casterPosition, float casterHeight) = DuelPlace(true);
            (Vector3 duelistPosition, float duelistHeight) = DuelPlace(false);
            bool casterAirborne = duelCaster != null && innerContainer.Contains(duelCaster);
            bool duelistAirborne = duelist != null && innerContainer.Contains(duelist);

            if (ageTicks >= DiveTick)
            {
                if (casterAirborne) DrawDuelShadow(casterPosition, casterHeight);
                if (duelistAirborne) DrawDuelShadow(duelistPosition, duelistHeight);
                float lanceLeft = 1f - BrushStroke.SmoothStep(LandStartTick, LandTick, ageTicks);
                if (casterAirborne && LanceIntact(true)) enhancedLanceLook.Draw(casterPosition, LanceFacing(true, casterPosition), lanceLeft, LanceAltitude(casterHeight, airAltitude));
                if (duelistAirborne && LanceIntact(false)) enhancedLanceLook.Draw(duelistPosition, LanceFacing(false, duelistPosition), lanceLeft, LanceAltitude(duelistHeight, airAltitude));
                DrawDuelSpectacle(airAltitude);
                return;
            }

            Vector3 towardCaster = new Vector3(airPosition.x - duelistGround.x, 0f, airPosition.z - duelistGround.z).normalized;
            float morph = BrushStroke.SmoothStep(LanceGrownTick, DiveTick, ageTicks);
            float groundAltitude = AltitudeLayer.Pawn.AltitudeFor() + 0.1f;
            duelistLanceLook.Draw(duelistPosition, towardCaster, 1f - morph, groundAltitude);
            enhancedLanceLook.Draw(duelistPosition, towardCaster, morph, groundAltitude + 0.01f);
        }

        /// <summary>
        /// The loser's lance is gone from the moment it breaks.
        /// </summary>
        private bool LanceIntact(bool forCaster) => !duelResolved || ageTicks < BreakTick || forCaster == casterWonDuel;

        /// <summary>
        /// Straight at the opponent during the clashes; once the loser is run through, the winner's lance carries them,
        /// and after the slam it stays buried in them until the winner lets go.
        /// </summary>
        private Vector3 LanceFacing(bool forCaster, Vector3 ownPosition)
        {
            Vector3 facing = forCaster ? diveDirection : -diveDirection;
            if (!duelResolved || forCaster != casterWonDuel) return facing;
            Pawn loser = DuelLoser;
            if (ageTicks >= SlamTick && loser != null && loser.Spawned)
            {
                Vector3 toward = new Vector3(loser.DrawPos.x - ownPosition.x, 0f, loser.DrawPos.z - ownPosition.z);
                if (toward.sqrMagnitude > 0.01f) return toward.normalized;
            }
            return ImpaleDirection();
        }

        private static float LanceAltitude(float height, float airAltitude) => height > 0.05f ? airAltitude + 0.12f : AltitudeLayer.Pawn.AltitudeFor() + 0.1f;

        private static void DrawDuelShadow(Vector3 position, float height)
        {
            if (height <= 0.05f) return;
            Vector3 ground = position - new Vector3(0f, 0f, height);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(ground.WithY(AltitudeLayer.Shadows.AltitudeFor()), Quaternion.identity, new Vector3(1.2f, 1f, 0.6f)), ShadowMaterial, 0);
        }

        private void EnsureDuelLooks()
        {
            if (duelistLanceLook != null) return;
            CompProperties_AbilityLaSangre settings = Props;
            duelistLanceLook = new LanceLook(settings.interceptorLanceTexturePath, settings.interceptorLanceButtOnTexture, settings.interceptorLanceTipOnTexture, settings.interceptorLanceHalfWidthOnTexture, settings.interceptorLanceLength, settings.interceptorLanceGripFraction);
            enhancedLanceLook = new LanceLook(settings.lanceTexturePath, settings.lanceButtOnTexture, settings.lanceTipOnTexture, settings.lanceHalfWidthOnTexture, settings.lanceLength, settings.lanceGripFraction);
        }

        private void ExposeDuel()
        {
            Scribe_References.Look(ref duelCaster, "duelCaster");
            Scribe_References.Look(ref duelist, "duelist");
            Scribe_Values.Look(ref duelistGround, "duelistGround");
            Scribe_Values.Look(ref meetPoint, "meetPoint");
            Scribe_Values.Look(ref duelistStrength, "duelistStrength", 1f);
            Scribe_Values.Look(ref duelistWasDrafted, "duelistWasDrafted");
            Scribe_Values.Look(ref casterClashWins, "casterClashWins");
            Scribe_Values.Look(ref duelistClashWins, "duelistClashWins");
            Scribe_Values.Look(ref casterClashMask, "casterClashMask");
            Scribe_Values.Look(ref duelResolved, "duelResolved");
            Scribe_Values.Look(ref casterWonDuel, "casterWonDuel");
        }

        /// <summary>
        /// One textured lance drawn as a straight quad pointing along a direction, growing outward from the grip.
        /// The texture can be drawn at any angle: the butt and tip UVs say where its axis lies.
        /// </summary>
        private class LanceLook
        {
            private readonly Mesh mesh = new Mesh();
            private readonly Material material;
            private readonly Vector2 buttOnTexture;
            private readonly Vector2 tipOnTexture;
            private readonly float halfWidthOnTexture;
            private readonly float length;
            private readonly float gripFraction;
            private readonly float aspect;

            public LanceLook(string texturePath, Vector2 buttOnTexture, Vector2 tipOnTexture, float halfWidthOnTexture, float length, float gripFraction)
            {
                this.buttOnTexture = buttOnTexture;
                this.tipOnTexture = tipOnTexture;
                this.halfWidthOnTexture = halfWidthOnTexture;
                this.length = length;
                this.gripFraction = gripFraction;

                Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, false) ?? BaseContent.BadTex;
                Vector2 axis = tipOnTexture - buttOnTexture;
                Vector2 side = new Vector2(-axis.y, axis.x).normalized * halfWidthOnTexture;
                float axisPixels = new Vector2(axis.x * texture.width, axis.y * texture.height).magnitude;
                float sidePixels = new Vector2(side.x * texture.width, side.y * texture.height).magnitude;
                aspect = 2f * sidePixels / Mathf.Max(1f, axisPixels);
                material = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, Color.white));
                mesh.vertices = new Vector3[4];
                mesh.uv = new Vector2[4];
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            }

            public void Draw(Vector3 grip, Vector3 direction, float grown, float altitude, float opacity = 1f)
            {
                if (grown <= 0f || direction.sqrMagnitude < 1e-6f) return;
                float halfHeight = length * aspect / 2f;
                float back = -gripFraction * length;
                float front = (grown - gripFraction) * length;
                mesh.vertices = new[] { new Vector3(back, 0f, -halfHeight), new Vector3(back, 0f, halfHeight), new Vector3(front, 0f, halfHeight), new Vector3(front, 0f, -halfHeight) };
                mesh.uv = new[] { Uv(0f, -1f), Uv(0f, 1f), Uv(grown, 1f), Uv(grown, -1f) };
                mesh.RecalculateBounds();
                Quaternion turn = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(0f, -90f, 0f);
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(grip.WithY(altitude), turn, Vector3.one), FadedMaterialPool.FadedVersionOf(material, opacity), 0);
            }

            private Vector2 Uv(float along, float side)
            {
                Vector2 axis = tipOnTexture - buttOnTexture;
                Vector2 left = new Vector2(-axis.y, axis.x).normalized * halfWidthOnTexture;
                return buttOnTexture + axis * along + left * side;
            }
        }
    }

    /// <summary>
    /// The duels in progress, so other systems can leave their pawns and lances alone: the interceptor's lance doesn't need
    /// upkeep while it's mid-duel, and neither duelist draws their own held weapon over the duel lances.
    /// </summary>
    public static class LaSangreDuels
    {
        private static readonly HashSet<LaSangreFlight> activeDuels = new HashSet<LaSangreFlight>();

        internal static void Register(LaSangreFlight flight) => activeDuels.Add(flight);

        internal static void Unregister(LaSangreFlight flight) => activeDuels.Remove(flight);

        public static bool IsDueling(Pawn pawn)
        {
            if (activeDuels.Count == 0) return false;
            activeDuels.RemoveWhere(flight => flight.Destroyed);
            return activeDuels.Any(flight => flight.DuelInvolves(pawn));
        }

        public static bool IsDuelLance(Thing thing)
        {
            if (activeDuels.Count == 0 || thing == null) return false;
            return activeDuels.Any(flight => flight.DuelistLance == thing);
        }
    }

    /// <summary>
    /// Duelists don't draw the weapon they're holding: the duel draws their lances itself.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.CarryWeaponOpenly))]
    public static class Patch_PawnRenderUtility_CarryWeaponOpenly_Duel
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && LaSangreDuels.IsDueling(pawn)) __result = false;
        }
    }
}