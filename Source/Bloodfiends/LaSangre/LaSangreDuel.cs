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
    /// pours all their hemogen into it, morphs it into Enhanced La Sangre and meets the dive mid-air. The lances lock and grind
    /// through a few clashes; then the winner impales the loser and slams them into the ground. The blast never lands.
    /// </summary>
    public partial class LaSangreFlight
    {
        private const int DashTicks = 8;
        private const int FirstClashTick = DiveTick + DashTicks;
        private const int ClashIntervalTicks = 8;
        private const int LungeTicks = 4;
        private const int HoldTicks = 4;
        private const int SlamTicks = 6;
        private const int LandingTicks = 10;
        private const int LesserGeneration = 8;
        private const float DuelHeightFactor = 0.6f;
        private const float ReachShare = 0.7f;
        private const float DriftPerClash = 0.3f;
        private const float JitterDistance = 0.08f;
        private const float LungeShare = 0.6f;
        private const float ImpaleShare = 0.8f;
        private const float StandOffShare = 0.6f;
        private const float SlamAheadDistance = 2.5f;
        private const float WinnerHangHeight = 1.2f;
        private const float ClashFlashSize = 3f;
        private const float ClashShake = 1.5f;
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
        private bool duelResolved;
        private bool casterWonDuel;

        private LanceLook duelistLanceLook;
        private LanceLook enhancedLanceLook;

        private int ResolveTick => FirstClashTick + Props.duelClashes * ClashIntervalTicks;

        private int ImpaleTick => ResolveTick + LungeTicks;

        private int SlamStartTick => ImpaleTick + HoldTicks;

        private int SlamTick => SlamStartTick + SlamTicks;

        private int LandTick => SlamTick + LandingTicks;

        private float DuelHeight => Props.airHeight * DuelHeightFactor;

        private float TipDistance => (1f - Props.lanceGripFraction) * Props.lanceLength;

        private float Reach => TipDistance * ReachShare;

        private Vector3 MeetGround => meetPoint - new Vector3(0f, 0f, DuelHeight);

        private Vector3 WinnerFacing => casterWonDuel ? diveDirection : -diveDirection;

        private Vector3 SlamPoint => casterWonDuel ? MeetGround : MeetGround + diveDirection * SlamAheadDistance;

        private Vector3 WinnerLanding => SlamPoint - WinnerFacing * TipDistance * StandOffShare;

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

            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_LanceForming, duelistGround, map);
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
            if (ageTicks == DiveTick)
            {
                BloodfiendsSounds.PlayBloodRush(airPosition, map);
                BloodfiendsSounds.PlayBloodRush(duelistGround, map);
            }

            int ticksSinceFirstClash = ageTicks - FirstClashTick;
            if (ticksSinceFirstClash >= 0 && ticksSinceFirstClash % ClashIntervalTicks == 0 && ticksSinceFirstClash / ClashIntervalTicks < Props.duelClashes) Clash(map, ticksSinceFirstClash / ClashIntervalTicks);
            if (ageTicks == ResolveTick)
            {
                duelResolved = true;
                casterWonDuel = casterClashWins > duelistClashWins;
            }
            if (ageTicks == ImpaleTick) Impale(map);
            if (ageTicks == SlamTick) Slam(map);
            if (ageTicks >= LandTick) LandDuel(map);
        }

        /// <summary>
        /// One clash in the lock. Who takes it is a roll weighted by each side's power; the contact point is shoved toward the loser.
        /// </summary>
        private void Clash(Map map, int clashIndex)
        {
            float casterPower = DuelPower(duelCaster, strength);
            float duelistPower = DuelPower(duelist, duelistStrength);
            if (Rand.Chance(casterPower / (casterPower + duelistPower))) casterClashWins++;
            else duelistClashWins++;

            Vector3 contact = Contact();
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, contact, 18, new FloatRange(0.8f, 1.6f), new FloatRange(0.16f, 0.26f), seed + 23 + clashIndex, 0.5f));
            FleckMaker.Static(contact, map, FleckDefOf.ShotFlash, ClashFlashSize);
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_LanceChargeHit, contact, map);
            Shake(map, ClashShake);
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
        /// The winner's lance runs the loser through; the loser's own lance shatters.
        /// </summary>
        private void Impale(Map map)
        {
            Vector3 loserPosition = DuelPlace(DuelLoser == duelCaster).position;
            Vector3 loserFacing = -WinnerFacing;
            Vector3 brokenMiddle = loserPosition + loserFacing * Reach * 0.5f;
            MapComponent_BrushLines.Start(map, new ShatterLineEffect(MapComponent_BloodCommand.BloodPalette, brokenMiddle, brokenMiddle - new Vector3(0f, 0f, DuelHeight), loserFacing.x >= 0f ? 1f : -1f, 12, 0.5f, 0.1f, seed + 31, 0.9f));
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, loserPosition, 24, new FloatRange(1f, 2f), new FloatRange(0.18f, 0.3f), seed + 37, 0.6f));
            FleckMaker.Static(loserPosition, map, FleckDefOf.ShotFlash, ClashFlashSize * 1.6f);
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodShatter, loserPosition, map);
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_LanceChargeHit, loserPosition, map);
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
            PutDownDuelPawn(loser, slamPoint, -WinnerFacing);

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
            if (casterWonDuel) BloodfiendsSounds.PlayDissolve(WinnerLanding, map);

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
        /// Where the lances meet: shoved a little toward whoever has lost more clashes.
        /// </summary>
        private Vector3 Contact() => meetPoint + diveDirection * (casterClashWins - duelistClashWins) * DriftPerClash;

        private Vector3 Jitter(float pawnSeed)
        {
            float x = Mathf.PerlinNoise(ageTicks * 0.6f, pawnSeed) - 0.5f;
            float z = Mathf.PerlinNoise(pawnSeed, ageTicks * 0.6f) - 0.5f;
            return new Vector3(x, 0f, z) * 2f * JitterDistance;
        }

        /// <summary>
        /// Where a duelist is drawn and how high off the ground (for the shadow): the dash, the grinding lock,
        /// then for the winner the lunge, slam and landing, and for the loser being run through and driven into the ground.
        /// </summary>
        private (Vector3 position, float height) DuelPlace(bool forCaster)
        {
            Vector3 facing = forCaster ? diveDirection : -diveDirection;
            Vector3 start = forCaster ? airPosition : duelistGround;
            float startHeight = forCaster ? Props.airHeight : 0f;
            if (ageTicks < DiveTick) return (start, startHeight);

            Vector3 locked = Contact() - facing * Reach;
            if (ageTicks < FirstClashTick)
            {
                float dash = Mathf.Pow(Mathf.InverseLerp(DiveTick, FirstClashTick, ageTicks), 2f);
                return (Vector3.Lerp(start, locked, dash), Mathf.Lerp(startHeight, DuelHeight, dash));
            }
            Vector3 jitter = Jitter(forCaster ? 1.7f : 4.3f);
            if (ageTicks < ResolveTick) return (locked + jitter, DuelHeight);

            bool isWinner = forCaster == casterWonDuel;
            Vector3 winnerLocked = Contact() - WinnerFacing * Reach;
            Vector3 winnerLunge = winnerLocked + WinnerFacing * Reach * LungeShare;
            Vector3 impaled = winnerLunge + WinnerFacing * TipDistance * ImpaleShare;
            if (isWinner)
            {
                Vector3 aboveSlam = WinnerLanding + new Vector3(0f, 0f, WinnerHangHeight);
                if (ageTicks < ImpaleTick) return (Vector3.Lerp(winnerLocked, winnerLunge, EaseOut(ResolveTick, ImpaleTick)), DuelHeight);
                if (ageTicks < SlamStartTick) return (winnerLunge + jitter, DuelHeight);
                if (ageTicks < SlamTick) return (Vector3.Lerp(winnerLunge, aboveSlam, EaseIn(SlamStartTick, SlamTick)), Mathf.Lerp(DuelHeight, WinnerHangHeight, EaseIn(SlamStartTick, SlamTick)));
                float drop = BrushStroke.SmoothStep(SlamTick, LandTick, ageTicks);
                return (Vector3.Lerp(aboveSlam, WinnerLanding, drop), Mathf.Lerp(WinnerHangHeight, 0f, drop));
            }

            if (ageTicks < ImpaleTick) return (Vector3.Lerp(locked, impaled, EaseOut(ResolveTick, ImpaleTick)), DuelHeight);
            if (ageTicks < SlamStartTick) return (impaled + jitter, DuelHeight);
            if (ageTicks < SlamTick) return (Vector3.Lerp(impaled, SlamPoint, EaseIn(SlamStartTick, SlamTick)), Mathf.Lerp(DuelHeight, 0f, EaseIn(SlamStartTick, SlamTick)));
            return (SlamPoint, 0f);
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
                if (casterAirborne && LanceIntact(true)) enhancedLanceLook.Draw(casterPosition, LanceFacing(true, casterPosition, duelistPosition), 1f, LanceAltitude(casterHeight, airAltitude));
                if (duelistAirborne && LanceIntact(false)) enhancedLanceLook.Draw(duelistPosition, LanceFacing(false, duelistPosition, casterPosition), 1f, LanceAltitude(duelistHeight, airAltitude));
                return;
            }

            Vector3 towardCaster = new Vector3(airPosition.x - duelistGround.x, 0f, airPosition.z - duelistGround.z).normalized;
            float morph = BrushStroke.SmoothStep(LanceGrownTick, DiveTick, ageTicks);
            float groundAltitude = AltitudeLayer.Pawn.AltitudeFor() + 0.1f;
            duelistLanceLook.Draw(duelistPosition, towardCaster, 1f - morph, groundAltitude);
            enhancedLanceLook.Draw(duelistPosition, towardCaster, morph, groundAltitude + 0.01f);
        }

        /// <summary>
        /// The loser's lance is gone from the moment they're run through.
        /// </summary>
        private bool LanceIntact(bool forCaster) => !duelResolved || ageTicks < ImpaleTick || forCaster == casterWonDuel;

        /// <summary>
        /// Straight at the opponent during the lock; once the winner lunges, their lance follows the loser down.
        /// </summary>
        private Vector3 LanceFacing(bool forCaster, Vector3 ownPosition, Vector3 opponentPosition)
        {
            Vector3 facing = forCaster ? diveDirection : -diveDirection;
            if (!duelResolved || forCaster != casterWonDuel) return facing;
            Vector3 target = ageTicks < SlamTick ? opponentPosition : SlamPoint;
            Vector3 toward = new Vector3(target.x - ownPosition.x, 0f, target.z - ownPosition.z);
            return toward.sqrMagnitude > 0.01f ? toward.normalized : facing;
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

            public void Draw(Vector3 grip, Vector3 direction, float grown, float altitude)
            {
                if (grown <= 0f || direction.sqrMagnitude < 1e-6f) return;
                float halfHeight = length * aspect / 2f;
                float back = -gripFraction * length;
                float front = (grown - gripFraction) * length;
                mesh.vertices = new[] { new Vector3(back, 0f, -halfHeight), new Vector3(back, 0f, halfHeight), new Vector3(front, 0f, halfHeight), new Vector3(front, 0f, -halfHeight) };
                mesh.uv = new[] { Uv(0f, -1f), Uv(0f, 1f), Uv(grown, 1f), Uv(grown, -1f) };
                mesh.RecalculateBounds();
                Quaternion turn = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(0f, -90f, 0f);
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(grip.WithY(altitude), turn, Vector3.one), material, 0);
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