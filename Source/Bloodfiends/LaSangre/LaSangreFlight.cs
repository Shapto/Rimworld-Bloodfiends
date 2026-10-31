using Bloodfiends.BloodCommand;
using Bloodfiends.Family;
using RimWorld;
using SingularityFramework.Visuals.Brushlines;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bloodfiends.LaSangre
{
    public class LaSangreFlight : Thing, IThingHolder
    {
        private const int CocoonClosedTick = 54;
        private const int HiddenTick = 40;
        private const int TeleportEndTick = 82;
        private const int AirCocoonStartTick = 72;
        private const int AirCocoonClosedTick = 104;
        private const int RevealTick = 108;
        private const int ShatterEndTick = 122;
        private const int LanceStartTick = 112;
        private const int LanceGrownTick = 140;
        private const int WindStartTick = 132;
        private const int DiveTick = 168;
        private const int ImpactTick = 174;

        private const float CocoonBottom = -0.55f;
        private const float CocoonTop = 1.6f;
        private const float CocoonHalfWidth = 0.42f;
        private const float TendrilRadius = 0.2f;

        private ThingOwner<Pawn> innerContainer;
        private int ageTicks;
        private Vector3 groundPosition;
        private Vector3 airPosition;
        private Vector3 impactPosition;
        private Vector3 landingPosition;
        private Vector3 diveDirection;
        private float strength = 1f;
        private bool wasDrafted;
        private int seed;
        private AbilityDef abilityDef;

        private List<(BrushStroke stroke, float delay)> groundCocoon;
        private List<(BrushStroke stroke, float delay)> airCocoon;
        private List<BrushStroke> teleportStreaks;
        private BrushLineDrawer groundDrawer;
        private BrushLineDrawer backDrawer;
        private BrushLineDrawer frontDrawer;
        private BrushLineDrawer highDrawer;
        private Mesh lanceMesh;
        private Material lanceMaterial;
        private Material ghostMaterial;
        private float lanceAspect = 0.2f;

        private static readonly Material ShadowMaterial = MaterialPool.MatFrom("Things/Skyfaller/SkyfallerShadowCircle", ShaderDatabase.Transparent);

        public LaSangreFlight()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }

        public Pawn FlyingPawn => innerContainer.Count > 0 ? innerContainer[0] : null;

        private CompProperties_AbilityLaSangre Props => abilityDef?.comps?.OfType<CompProperties_AbilityLaSangre>().FirstOrDefault() ?? new CompProperties_AbilityLaSangre();

        private float LanceAhead => 0f;

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public static void Launch(Pawn caster, IntVec3 targetCell, float strength, AbilityDef abilityDef)
        {
            Map map = caster.Map;
            var flight = (LaSangreFlight)ThingMaker.MakeThing(BloodfiendsDefOf.Bloodfiends_LaSangreFlight);
            flight.abilityDef = abilityDef;
            flight.strength = strength;
            flight.seed = Rand.Int;
            flight.wasDrafted = caster.Drafted;
            flight.PlanPositions(caster.DrawPos, targetCell);
            GenSpawn.Spawn(flight, caster.Position, map);
            caster.DeSpawn(DestroyMode.WillReplace);
            flight.innerContainer.TryAdd(caster);
        }

        private void PlanPositions(Vector3 casterPosition, IntVec3 targetCell)
        {
            groundPosition = casterPosition;
            impactPosition = targetCell.ToVector3Shifted();
            Vector3 approach = impactPosition - groundPosition;
            approach.y = 0f;
            approach = approach.sqrMagnitude > 0.01f ? approach.normalized : Vector3.forward;
            airPosition = impactPosition - approach * Props.airDistance + new Vector3(0f, 0f, Props.airHeight);
            diveDirection = impactPosition - airPosition;
            diveDirection.y = 0f;
            diveDirection.Normalize();
            landingPosition = impactPosition;
        }

        public override Vector3 DrawPos => ageTicks < TeleportEndTick ? groundPosition : airPosition;

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;
            Map map = Map;
            if (ageTicks == 1) BloodfiendsSounds.PlayAt(SoundDefOf.Psycast_Skip_Pulse, groundPosition, map);                  // blood winding into the cocoon
            if (ageTicks == CocoonClosedTick)
            {
                StartEruption();
                BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodForming, groundPosition, map);               // the cocoon seals
                BloodfiendsSounds.PlayAt(SoundDefOf.Psycast_Skip_Exit, groundPosition, map);                                // and is gone
            }
            if (ageTicks == AirCocoonStartTick) BloodfiendsSounds.PlayAt(SoundDefOf.Psycast_Skip_Entry, airPosition, map);
            if (ageTicks == RevealTick) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_HardbloodShatter, airPosition, map);
            if (ageTicks == LanceStartTick) BloodfiendsSounds.PlayAt(BloodfiendsDefOf.Bloodfiends_LanceForming, airPosition, map);
            if (ageTicks == DiveTick)
            {
                StartSmear();
                BloodfiendsSounds.PlayBloodRush(airPosition, map);
            }
            if (ageTicks >= ImpactTick) Impact();
        }

        private void StartEruption()
        {
            var eruption = new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, groundPosition + new Vector3(0f, 0f, 0.5f), 20, new FloatRange(1.3f, 2.2f), new FloatRange(0.2f, 0.32f), seed + 11, 0.6f);
            MapComponent_BrushLines.Start(Map, eruption);
        }

        private void StartSmear()
        {
            MapComponent_BrushLines.Start(Map, new SmearLineEffect(MapComponent_BloodCommand.BloodPalette, airPosition, impactPosition + diveDirection * 0.5f, seed + 13, 0.5f));
        }

        private void Impact()
        {
            Map map = Map;
            Pawn caster = FlyingPawn;
            IntVec3 impactCell = impactPosition.ToIntVec3();
            CompProperties_AbilityLaSangre settings = Props;

            var enemiesHit = new List<Pawn>();
            if (caster != null)
            {
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(impactCell, map, settings.impactRadius, true).ToList())
                {
                    if (!(thing is Pawn enemy) || enemy == caster || enemy.Dead || !enemy.HostileTo(caster)) continue;
                    Vector3 away = enemy.DrawPos - impactPosition;
                    float angle = away.sqrMagnitude > 0.01f ? away.AngleFlat() : diveDirection.AngleFlat();
                    enemy.TakeDamage(new DamageInfo(settings.damageDef ?? DamageDefOf.Stab, settings.damage * strength, settings.armorPenetration, angle, caster));
                    if (!enemy.Dead) enemy.stances?.stunner?.StunFor(settings.stunTicks, caster);
                    enemiesHit.Add(enemy);
                }
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(impactCell, settings.impactRadius * 1.3f, true))
            {
                if (cell.InBounds(map) && Rand.Chance(0.55f)) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood, Rand.RangeInclusive(1, 3));
            }
            MapComponent_BrushLines.Start(map, new BurstLineEffect(MapComponent_BloodCommand.BloodPalette, impactPosition, 44, new FloatRange(settings.impactRadius * 0.9f, settings.impactRadius * 1.9f), new FloatRange(0.22f, 0.38f), seed + 17, 0.8f));
            StartLanceDissolve(map);
            FleckMaker.Static(impactPosition, map, FleckDefOf.ExplosionFlash, settings.impactRadius * 2.5f);
            if (map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(settings.cameraShake);
            settings.impactSound?.PlayOneShot(new TargetInfo(impactCell, map));

            if (caster != null)
            {
                FeedFamily(caster, enemiesHit, map);
                PutDown(caster, map);
            }
            Destroy();
        }

        private void StartLanceDissolve(Map map)
        {
            Vector3 middle = impactPosition + diveDirection * 1f;
            float side = diveDirection.x >= 0f ? 1f : -1f;
            var dissolve = new ShatterLineEffect(MapComponent_BloodCommand.BloodPalette, middle, impactPosition, side, 10, 0.4f, 0.1f, seed + 19, 0.9f);
            MapComponent_BrushLines.Start(map, dissolve);
            BloodfiendsSounds.PlayDissolve(impactPosition, map);
        }

        private void FeedFamily(Pawn caster, List<Pawn> enemiesHit, Map map)
        {
            if (enemiesHit.Count == 0) return;
            GameComponent_BloodfiendFamilies families = GameComponent_BloodfiendFamilies.Instance;
            if (families == null) return;
            List<Pawn> kindred = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != caster && !pawn.Dead && pawn.Position.InHorDistOf(impactPosition.ToIntVec3(), Props.familyRadius))
                .Where(pawn => pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>() != null && families.AreSameFamily(caster, pawn))
                .ToList();
            if (kindred.Count == 0) return;

            float share = enemiesHit.Count * Props.hemogenStolenPerEnemy * strength / kindred.Count;
            foreach (Pawn kin in kindred)
            {
                for (int index = 0; index < enemiesHit.Count; index++)
                {
                    var stream = new StreamLineEffect(MapComponent_BloodCommand.BloodPalette, enemiesHit[index].DrawPos, kin, 0.12f, false, seed + index * 31 + kin.thingIDNumber, 0.9f);
                    if (index == 0) stream.AtMoment(1f, () => { if (!kin.Dead) GeneUtility.OffsetHemogen(kin, share); });
                    MapComponent_BrushLines.Start(map, stream);
                }
            }
        }

        private void PutDown(Pawn caster, Map map)
        {
            IntVec3 cell = landingPosition.ToIntVec3();
            if (!cell.InBounds(map) || !cell.Standable(map)) cell = CellFinder.StandableCellNear(impactPosition.ToIntVec3(), map, 4f);
            if (!cell.IsValid) cell = Position;
            innerContainer.Remove(caster);
            GenSpawn.Spawn(caster, cell, map, WipeMode.Vanish);
            caster.Rotation = Rot4.FromAngleFlat(diveDirection.AngleFlat());
            if (caster.drafter != null && wasDrafted) caster.drafter.Drafted = true;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Pawn caster = FlyingPawn;
            if (caster != null && Spawned) PutDown(caster, Map);
            base.Destroy(mode);
        }


        private bool PawnVisible => ageTicks < HiddenTick || (ageTicks >= RevealTick && ageTicks < DiveTick);

        private Vector3 PawnDrawPosition => ageTicks < TeleportEndTick ? groundPosition.WithY(AltitudeLayer.Pawn.AltitudeFor()) : airPosition.WithY(AltitudeLayer.Skyfaller.AltitudeFor());

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            Pawn pawn = FlyingPawn;
            if (pawn != null && PawnVisible) pawn.DynamicDrawPhaseAt(phase, PawnDrawPosition, flip);
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            EnsureLooks();
            float airAltitude = AltitudeLayer.Skyfaller.AltitudeFor();

            if (ageTicks >= RevealTick - 10 && ageTicks < DiveTick)
            {
                Vector3 ground = airPosition - new Vector3(0f, 0f, Props.airHeight);
                Matrix4x4 shadow = Matrix4x4.TRS(ground.WithY(AltitudeLayer.Shadows.AltitudeFor()), Quaternion.identity, new Vector3(1.2f, 1f, 0.6f));
                Graphics.DrawMesh(MeshPool.plane10, shadow, ShadowMaterial, 0);
            }

            DrawGroundCocoon();
            DrawTeleport();
            DrawAirCocoon();
            if (ageTicks >= LanceStartTick && ageTicks < DiveTick)
            {
                float grown = BrushStroke.SmoothStep(LanceStartTick, LanceGrownTick, ageTicks);
                DrawLance(GripPosition(airPosition), grown, lanceMaterial, airAltitude + 0.12f);
            }
            if (ageTicks >= WindStartTick && ageTicks < DiveTick) DrawTendrils();
            if (ageTicks >= DiveTick) DrawGhosts(airAltitude + 0.12f);

            groundDrawer.DrawAll();
            backDrawer.DrawAll();
            frontDrawer.DrawAll();
            highDrawer.DrawAll();
        }

        private Vector3 GripPosition(Vector3 pawnPosition) => pawnPosition + diveDirection * LanceAhead;

        private void DrawGroundCocoon()
        {
            if (ageTicks >= CocoonClosedTick) return;
            foreach (var entry in groundCocoon)
            {
                float local = (ageTicks / (float)CocoonClosedTick - entry.delay) / (1f - entry.delay);
                if (local <= 0f) continue;
                entry.stroke.Draw(groundDrawer, BrushStroke.SmoothStep(0f, 0.75f, local), 0.3f * BrushStroke.SmoothStep(0.6f, 0.95f, local), 1f, 0f);
            }
        }

        private void DrawTeleport()
        {
            if (ageTicks < CocoonClosedTick || ageTicks >= TeleportEndTick + 6) return;
            float local = (ageTicks - CocoonClosedTick) / (float)(TeleportEndTick + 6 - CocoonClosedTick);
            foreach (BrushStroke stroke in teleportStreaks) stroke.Draw(highDrawer, BrushStroke.SmoothStep(0f, 0.5f, local), BrushStroke.SmoothStep(0.4f, 1f, local), 1f, 0f);
        }

        private void DrawAirCocoon()
        {
            if (ageTicks < AirCocoonStartTick || ageTicks >= ShatterEndTick) return;
            foreach (var entry in airCocoon)
            {
                BrushStroke stroke = entry.stroke;
                if (ageTicks < AirCocoonClosedTick)
                {
                    float local = ((ageTicks - AirCocoonStartTick) / (float)(AirCocoonClosedTick - AirCocoonStartTick) - entry.delay) / (1f - entry.delay);
                    if (local > 0f) stroke.Draw(highDrawer, BrushStroke.SmoothStep(0f, 1f, local), 0f, 1f, 0f);
                }
                else
                {
                    float local = (ageTicks - AirCocoonClosedTick) / (float)(ShatterEndTick - AirCocoonClosedTick);
                    stroke.Draw(highDrawer, 1f, BrushStroke.SmoothStep(0.3f, 1f, local), 1f - BrushStroke.SmoothStep(0.5f, 1f, local), BrushStroke.SmoothStep(0f, 0.4f, local));
                }
            }
        }

        private void DrawLance(Vector3 grip, float grown, Material material, float altitude)
        {
            if (grown <= 0f) return;
            float length = Props.lanceLength;
            float halfHeight = length * lanceAspect / 2f;
            float back = -Props.lanceGripFraction * length;
            float front = (grown - Props.lanceGripFraction) * length;
            lanceMesh.vertices = new[] { new Vector3(back, 0f, -halfHeight), new Vector3(back, 0f, halfHeight), new Vector3(front, 0f, halfHeight), new Vector3(front, 0f, -halfHeight) };
            lanceMesh.uv = new[] { LanceUv(0f, -1f), LanceUv(0f, 1f), LanceUv(grown, 1f), LanceUv(grown, -1f) };
            lanceMesh.RecalculateBounds();
            Quaternion turn = Quaternion.FromToRotation(Vector3.right, diveDirection);
            Graphics.DrawMesh(lanceMesh, Matrix4x4.TRS(grip.WithY(altitude), turn, Vector3.one), material, 0);
        }

        private Vector2 LanceUv(float along, float side)
        {
            Vector2 axis = Props.lanceTipOnTexture - Props.lanceButtOnTexture;
            Vector2 left = new Vector2(-axis.y, axis.x).normalized * Props.lanceHalfWidthOnTexture;
            return Props.lanceButtOnTexture + axis * along + left * side;
        }

        private void DrawTendrils()
        {
            float windUp = (ageTicks - WindStartTick) / (float)(DiveTick - WindStartTick);
            float wrapped = BrushStroke.SmoothStep(0f, 0.55f, windUp);
            float spin = windUp * windUp * 40f;
            float length = Props.lanceLength;
            Vector3 butt = GripPosition(airPosition) - diveDirection * Props.lanceGripFraction * length;
            Vector3 across = new Vector3(-diveDirection.z, 0f, diveDirection.x);
            float guard = Props.lanceGuardFraction;
            const int Steps = 90;
            const float HalfWidth = 0.03f;
            int drawnSteps = Mathf.RoundToInt(Steps * wrapped);
            if (drawnSteps < 2) return;
            float drawnTo = drawnSteps / (float)Steps;

            var centres = new Vector3[drawnSteps + 1];
            var inFront = new bool[drawnSteps + 1];
            var tapers = new float[drawnSteps + 1];
            for (int helix = 0; helix < 3; helix++)
            {
                float phase = helix * Mathf.PI * 2f / 3f + spin;
                for (int step = 0; step <= drawnSteps; step++)
                {
                    float share = step / (float)Steps;
                    float along = guard + (1f - guard) * share;
                    float angle = phase + share * 7f * Mathf.PI;
                    float radius = Props.tendrilRadiusByAlong?.Evaluate(along) ?? TendrilRadius * (1.15f - 0.9f * share);
                    centres[step] = butt + diveDirection * length * along + across * Mathf.Sin(angle) * radius;
                    inFront[step] = Mathf.Cos(angle) > 0f;
                    tapers[step] = BrushStroke.SmoothStep(0f, 0.06f, share) * BrushStroke.SmoothStep(0f, 0.14f, drawnTo - share);
                }

                Vector3 previousLeft = Vector3.zero, previousRight = Vector3.zero;
                for (int step = 0; step <= drawnSteps; step++)
                {
                    Vector3 ahead = centres[Mathf.Min(step + 1, drawnSteps)] - centres[Mathf.Max(step - 1, 0)];
                    ahead.y = 0f;
                    Vector3 direction = ahead.sqrMagnitude > 1e-8f ? ahead.normalized : diveDirection;
                    Vector3 side = new Vector3(-direction.z, 0f, direction.x) * (HalfWidth * tapers[step]);
                    Vector3 left = centres[step] + side, right = centres[step] - side;
                    if (step > 0)
                    {
                        bool front = inFront[step - 1];
                        (front ? frontDrawer : backDrawer).AddQuad(front ? 0 : 2, front ? 1f : 0.7f, previousLeft, left, right, previousRight);
                    }
                    previousLeft = left;
                    previousRight = right;
                }
            }
        }

        private void DrawGhosts(float altitude)
        {
            for (int index = 0; index < 7; index++)
            {
                float share = index / 6f;
                Vector3 pawnPosition = Vector3.Lerp(airPosition, landingPosition, share);
                DrawLance(GripPosition(pawnPosition), 1f, ghostMaterial, altitude);
            }
        }

        private void EnsureLooks()
        {
            if (groundDrawer != null) return;
            BrushPalette palette = MapComponent_BloodCommand.BloodPalette;
            float airAltitude = AltitudeLayer.Skyfaller.AltitudeFor();
            groundDrawer = new BrushLineDrawer(palette, AltitudeLayer.Pawn.AltitudeFor() + 0.2f);
            backDrawer = new BrushLineDrawer(palette, airAltitude + 0.06f);
            frontDrawer = new BrushLineDrawer(palette, airAltitude + 0.18f);
            highDrawer = new BrushLineDrawer(palette, airAltitude + 0.24f);

            var random = new System.Random(seed);
            groundCocoon = BuildCocoon(groundPosition, random, true);
            airCocoon = BuildCocoon(airPosition, random, false);
            teleportStreaks = new List<BrushStroke>();
            Vector3 start = groundPosition + new Vector3(0f, 0f, 0.5f);
            Vector3 end = airPosition + new Vector3(0f, 0f, 0.5f);
            Vector3 middle = (start + end) / 2f + new Vector3(0f, 0f, 1.2f);
            foreach (float width in new[] { 0.34f, 0.2f, 0.2f })
            {
                teleportStreaks.Add(new BrushStroke(along => (1 - along) * (1 - along) * start + 2 * (1 - along) * along * middle + along * along * end, width, random.Next(), 6, 0.3f));
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(Props.lanceTexturePath, false) ?? BaseContent.BadTex;
            Vector2 axis = Props.lanceTipOnTexture - Props.lanceButtOnTexture;
            Vector2 side = new Vector2(-axis.y, axis.x).normalized * Props.lanceHalfWidthOnTexture;
            float axisPixels = new Vector2(axis.x * texture.width, axis.y * texture.height).magnitude;
            float sidePixels = new Vector2(side.x * texture.width, side.y * texture.height).magnitude;
            lanceAspect = 2f * sidePixels / Mathf.Max(1f, axisPixels);
            lanceMaterial = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, Color.white));
            ghostMaterial = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, new Color(1f, 1f, 1f, 0.15f)));
            lanceMesh = new Mesh();
            lanceMesh.vertices = new Vector3[4];
            lanceMesh.uv = new Vector2[4];
            lanceMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }

        private static List<(BrushStroke stroke, float delay)> BuildCocoon(Vector3 centre, System.Random random, bool fromGround)
        {
            var strands = new List<(BrushStroke stroke, float delay)>();
            Vector3 bottom = centre + new Vector3(0f, 0f, CocoonBottom);
            Vector3 apex = centre + new Vector3(0f, 0f, CocoonTop);
            for (int index = 0; index < 18; index++)
            {
                float side = index % 2 == 0 ? 1f : -1f;
                Vector3 groundStart = centre + new Vector3(side * Range(random, 0.6f, 1.6f), 0f, Range(random, -0.7f, 0.2f));
                float phase = Range(random, 0f, Mathf.PI * 2f);
                float turns = Range(random, 0.8f, 1.4f) * side;
                float runIn = fromGround ? 0.3f : 0f;
                System.Func<float, Vector3> path = along =>
                {
                    if (along < runIn)
                    {
                        float share = along / runIn;
                        return Vector3.Lerp(groundStart, bottom, share) + new Vector3(0f, 0f, Mathf.Sin(share * Mathf.PI / 2f) * 0.5f * (1f - share));
                    }
                    float climb = runIn > 0f ? (along - runIn) / (1f - runIn) : along;
                    Vector3 height = Vector3.Lerp(bottom, apex, climb);
                    float halfWidth = CocoonHalfWidth * Mathf.Pow(Mathf.Sin(Mathf.PI * climb), 0.7f);
                    return height + new Vector3(Mathf.Sin(phase + climb * turns * Mathf.PI * 2f) * halfWidth, 0f, 0f);
                };
                strands.Add((new BrushStroke(path, Range(random, 0.16f, 0.24f), random.Next(), 5, 0.25f), Range(random, 0f, 0.04f)));
            }
            return strands;
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref groundPosition, "groundPosition");
            Scribe_Values.Look(ref airPosition, "airPosition");
            Scribe_Values.Look(ref impactPosition, "impactPosition");
            Scribe_Values.Look(ref landingPosition, "landingPosition");
            Scribe_Values.Look(ref diveDirection, "diveDirection");
            Scribe_Values.Look(ref strength, "strength", 1f);
            Scribe_Values.Look(ref wasDrafted, "wasDrafted");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Defs.Look(ref abilityDef, "abilityDef");
        }
    }
}