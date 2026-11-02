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

namespace Bloodfiends.LaSangre
{
    /// <summary>
    /// The La Sangre duel. When La Sangre would strike a Bloodfiend holding a formed lance, the best melee fighter among them
    /// pours all their hemogen into an Enhanced shell over their lance and meets the dive mid-air. A few clashes, then a final
    /// blow: the loser's lance shatters and they take the brunt, and the blast never reaches the ground.
    /// </summary>
    public partial class LaSangreFlight
    {
        private const int FirstClashTick = DiveTick + 12;
        private const int ClashIntervalTicks = 16;
        private const int LandingTicks = 16;
        private const int LesserGeneration = 8;
        private const float DuelHeightFactor = 0.6f;
        private const float ReachShare = 0.7f;
        private const float RecoilDistance = 0.9f;
        private const float ClashFlashSize = 3f;
        private const float ClashShake = 1.5f;
        private const float DecisiveScale = 1.6f;

        private Pawn duelist;
        private Vector3 duelistGround;
        private Vector3 meetPoint;
        private Vector3 casterLanding;
        private float duelistStrength = 1f;
        private bool duelistWasDrafted;
        private int casterClashWins;
        private int duelistClashWins;
        private bool duelResolved;
        private bool casterWonDuel;

        private LanceLook duelistLanceLook;
        private LanceLook enhancementLook;

        private int ResolveTick => FirstClashTick + Props.duelClashes * ClashIntervalTicks;

        private int LandTick => ResolveTick + LandingTicks;

        private float DuelHeight => Props.airHeight * DuelHeightFactor;

        private float CasterReach => (1f - Props.lanceGripFraction) * Props.lanceLength * ReachShare;

        private float DuelistReach => (1f - Props.interceptorLanceGripFraction) * Props.interceptorLanceLength * ReachShare;

        internal bool DuelInvolves(Pawn pawn) => duelist != null && pawn != null && (pawn == duelist || pawn == FlyingPawn);

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

            duelist = interceptor;
            duelistGround = interceptor.DrawPos;
            duelistWasDrafted = interceptor.Drafted;
            Vector3 airGround = airPosition - new Vector3(0f, 0f, Props.airHeight);
            meetPoint = Vector3.Lerp(airGround, impactPosition, 0.5f) + new Vector3(0f, 0f, DuelHeight);
            casterLanding = Vector3.Lerp(airGround, impactPosition, 0.35f);
            landingPosition = casterLanding;

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
            if (ageTicks == DiveTick)
            {
                BloodfiendsSounds.PlayBloodRush(airPosition, map);
                BloodfiendsSounds.PlayBloodRush(duelistGround, map);
            }

            int ticksSinceFirstClash = ageTicks - FirstClashTick;
            if (ticksSinceFirstClash >= 0 && ticksSinceFirstClash % ClashIntervalTicks == 0 && ticksSinceFirstClash / ClashIntervalTicks < Props.duelClashes) Clash(map, ticksSinceFirstClash / ClashIntervalTicks);
            if (ageTicks == ResolveTick) Resolve(map);
            if (ageTicks >= LandTick) LandDuel(map);
        }

        /// <summary>
        /// One meeting of the lances. Who takes it is a roll weighted by each side's power.
        /// </summary>
        private void Clash(Map map, int clashIndex)
        {
            float casterPower = DuelPower(FlyingPawn, strength);
            float duelistPower = DuelPower(duelist, duelistStrength);
            if (Rand.Chance(casterPower / (casterPower + duelistPower))) casterClashWins++;
            else duelistClashWins++;
            ClashEffects(map, clashIndex, 1f);
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

        private void ClashEffects(Map map, int effectIndex, float scale)
        {
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, meetPoint, Mathf.RoundToInt(18 * scale), new FloatRange(0.8f * scale, 1.6f * scale), new FloatRange(0.16f, 0.26f), seed + 23 + effectIndex, 0.5f));
            FleckMaker.Static(meetPoint, map, FleckDefOf.ShotFlash, ClashFlashSize * scale);
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_LanceChargeHit, meetPoint, map);
            if (map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(ClashShake * scale);
            FilthMaker.TryMakeFilth((meetPoint - new Vector3(0f, 0f, DuelHeight)).ToIntVec3(), map, ThingDefOf.Filth_Blood, 2);
        }

        /// <summary>
        /// The decisive blow: whoever took more clashes breaks the other's lance.
        /// </summary>
        private void Resolve(Map map)
        {
            duelResolved = true;
            casterWonDuel = casterClashWins > duelistClashWins;
            ClashEffects(map, Props.duelClashes, DecisiveScale);

            Vector3 loserPosition = casterWonDuel ? DuelistPlace().position : CasterDuelPlace().position;
            Vector3 loserFacing = casterWonDuel ? -diveDirection : diveDirection;
            float loserReach = casterWonDuel ? DuelistReach : CasterReach;
            Vector3 brokenMiddle = loserPosition + loserFacing * loserReach * 0.5f;
            MapComponent_BrushLines.Start(map, new ShatterLineEffect(MapComponent_BloodCommand.BloodPalette, brokenMiddle, brokenMiddle - new Vector3(0f, 0f, DuelHeight), loserFacing.x >= 0f ? 1f : -1f, 12, 0.5f, 0.1f, seed + 31, 0.9f));
            BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodShatter, meetPoint, map);
        }

        private void LandDuel(Map map)
        {
            Pawn caster = FlyingPawn;
            Pawn interceptor = duelist;
            Thing duelistLance = DuelistLance;

            PutDownDuelist(map);
            if (caster != null) PutDown(caster, map);
            LaSangreDuels.Unregister(this);
            duelist = null;

            if (casterWonDuel)
            {
                if (duelistLance != null) GameComponent_HardbloodReplicas.Instance?.Shatter(duelistLance);
                BloodfiendsSounds.PlayDissolve(casterLanding, map);
            }

            Pawn winner = casterWonDuel ? caster : interceptor;
            Pawn loser = casterWonDuel ? interceptor : caster;
            float winnerStrength = casterWonDuel ? strength : duelistStrength;
            float loserStrength = casterWonDuel ? duelistStrength : strength;
            Strike(loser, winner, Props.damage * winnerStrength * Props.duelLoserDamageFactor, stun: true);
            Strike(winner, loser, Props.damage * loserStrength * Props.duelWinnerDamageFactor, stun: false);
            if (winner != null && loser != null) Messages.Message("Bloodfiends_LaSangreDuelWon".Translate(winner.Named("WINNER"), loser.Named("LOSER")), new LookTargets(winner), MessageTypeDefOf.NeutralEvent);
            Destroy();
        }

        private void Strike(Pawn victim, Pawn attacker, float damage, bool stun)
        {
            if (victim == null || victim.Dead || !victim.Spawned) return;
            victim.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Stab, damage, Props.armorPenetration, -1f, attacker));
            if (stun && !victim.Dead) victim.stances?.stunner?.StunFor(Props.stunTicks, attacker);
        }

        private void PutDownDuelist(Map map)
        {
            if (duelist == null || !innerContainer.Contains(duelist)) return;
            IntVec3 cell = duelistGround.ToIntVec3();
            if (!cell.InBounds(map) || !cell.Standable(map)) cell = CellFinder.StandableCellNear(cell, map, 4f);
            if (!cell.IsValid) cell = Position;
            innerContainer.Remove(duelist);
            GenSpawn.Spawn(duelist, cell, map, WipeMode.Vanish);
            duelist.Rotation = Rot4.FromAngleFlat((-diveDirection).AngleFlat());
            if (duelist.drafter != null && duelistWasDrafted) duelist.drafter.Drafted = true;
        }

        /// <summary>
        /// If the flight is destroyed mid-duel (the map closing, say), the interceptor is put back where they stood.
        /// </summary>
        private void EndDuelEarly()
        {
            if (Spawned) PutDownDuelist(Map);
            LaSangreDuels.Unregister(this);
            duelist = null;
        }

        private float Recoil()
        {
            float phase = ((ageTicks - FirstClashTick) % ClashIntervalTicks) / (float)ClashIntervalTicks;
            return Mathf.Sin(phase * Mathf.PI) * RecoilDistance;
        }

        /// <summary>
        /// Where the caster is drawn during the duel, and how high off the ground (for the shadow).
        /// </summary>
        private (Vector3 position, float height) CasterDuelPlace()
        {
            Vector3 contact = meetPoint - diveDirection * CasterReach;
            if (ageTicks < DiveTick) return (airPosition, Props.airHeight);
            if (ageTicks < FirstClashTick)
            {
                float dash = BrushStroke.SmoothStep(DiveTick, FirstClashTick, ageTicks);
                return (Vector3.Lerp(airPosition, contact, dash), Mathf.Lerp(Props.airHeight, DuelHeight, dash));
            }
            if (ageTicks < ResolveTick) return (contact - diveDirection * Recoil(), DuelHeight);
            float fall = BrushStroke.SmoothStep(ResolveTick, LandTick, ageTicks);
            return (Vector3.Lerp(contact, casterLanding, fall), Mathf.Lerp(DuelHeight, 0f, fall));
        }

        /// <summary>
        /// Where the interceptor is drawn: on the ground raising their lance, leaping up to meet the dive, then falling back.
        /// </summary>
        private (Vector3 position, float height) DuelistPlace()
        {
            Vector3 contact = meetPoint + diveDirection * DuelistReach;
            if (ageTicks < DiveTick) return (duelistGround, 0f);
            if (ageTicks < FirstClashTick)
            {
                float leap = BrushStroke.SmoothStep(DiveTick, FirstClashTick, ageTicks);
                return (Vector3.Lerp(duelistGround, contact, leap), Mathf.Lerp(0f, DuelHeight, leap));
            }
            if (ageTicks < ResolveTick) return (contact + diveDirection * Recoil(), DuelHeight);
            float fall = BrushStroke.SmoothStep(ResolveTick, LandTick, ageTicks);
            return (Vector3.Lerp(contact, duelistGround, fall), Mathf.Lerp(DuelHeight, 0f, fall));
        }

        private void DrawDuelistPawn(DrawPhase phase, bool flip)
        {
            if (duelist == null) return;
            float altitude = ageTicks < DiveTick ? AltitudeLayer.Pawn.AltitudeFor() : AltitudeLayer.Skyfaller.AltitudeFor();
            duelist.DynamicDrawPhaseAt(phase, DuelistPlace().position.WithY(altitude), flip);
        }

        private void DrawDuel(float airAltitude)
        {
            EnsureDuelLooks();
            (Vector3 casterPosition, float casterHeight) = CasterDuelPlace();
            (Vector3 duelistPosition, float duelistHeight) = DuelistPlace();

            if (ageTicks >= DiveTick)
            {
                DrawDuelShadow(casterPosition, casterHeight);
                DrawDuelShadow(duelistPosition, duelistHeight);
                if (!duelResolved || casterWonDuel) DrawLance(GripPosition(casterPosition), 1f, lanceMaterial, airAltitude + 0.12f);
            }
            if (duelResolved && casterWonDuel) return;

            Vector3 raised = new Vector3(meetPoint.x - duelistGround.x, 0f, meetPoint.z - duelistGround.z).normalized;
            Vector3 facing = Vector3.Slerp(raised, -diveDirection, BrushStroke.SmoothStep(DiveTick, FirstClashTick, ageTicks));
            float altitude = ageTicks < DiveTick ? AltitudeLayer.Pawn.AltitudeFor() + 0.1f : airAltitude + 0.12f;
            duelistLanceLook.Draw(duelistPosition, facing, 1f, altitude);

            float enhancementGrown = BrushStroke.SmoothStep(LanceGrownTick, DiveTick, ageTicks);
            if (duelResolved) enhancementGrown *= 1f - BrushStroke.SmoothStep(ResolveTick, LandTick, ageTicks);
            enhancementLook.Draw(duelistPosition, facing, enhancementGrown, altitude + 0.01f);
        }

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
            enhancementLook = new LanceLook(settings.enhancementTexturePath, settings.enhancementButtOnTexture, settings.enhancementTipOnTexture, settings.enhancementHalfWidthOnTexture, settings.interceptorLanceLength, settings.interceptorLanceGripFraction);
        }

        private void ExposeDuel()
        {
            Scribe_References.Look(ref duelist, "duelist");
            Scribe_Values.Look(ref duelistGround, "duelistGround");
            Scribe_Values.Look(ref meetPoint, "meetPoint");
            Scribe_Values.Look(ref casterLanding, "casterLanding");
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