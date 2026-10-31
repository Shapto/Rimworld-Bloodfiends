using Bloodfiends.Family;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hardblood
{
    public class HardbloodWeaponryExtension : DefModExtension
    {
        public SimpleCurve slotsByGeneration = new SimpleCurve { new CurvePoint(1f, 4f), new CurvePoint(2f, 3f), new CurvePoint(3f, 2f), new CurvePoint(4f, 2f), new CurvePoint(5f, 1f), new CurvePoint(7f, 1f) };
        public List<int> extraSlotCraftingLevels = new List<int> { 8, 15 };
        public SimpleCurve hemogenCostByMarketValue = new SimpleCurve { new CurvePoint(0f, 5f), new CurvePoint(300f, 10f), new CurvePoint(1000f, 18f), new CurvePoint(3000f, 25f) };
        public SimpleCurve costMultiplierByGeneration = new SimpleCurve { new CurvePoint(1f, 0.5f), new CurvePoint(7f, 1.2f) };
        public float upkeepPerDay = 6f;
        public SimpleCurve qualityByGeneration = new SimpleCurve { new CurvePoint(1f, 4f), new CurvePoint(4f, 2f), new CurvePoint(7f, 1f) };
        public int craftingLevelsPerQualityStep = 10;
        public QualityCategory maximumQuality = QualityCategory.Masterwork;
        public float formSeconds = 0.8f;
        public float lineWidth = 0.12f;
        public float hemogenPerShotDamage = 0.15f;                             
        public List<string> extraMimicableCompClasses = new List<string>();
        public List<HardbloodShardVariant> shardVariants = new List<HardbloodShardVariant>
        {
            new HardbloodShardVariant { texturePath = "Hardblood/Crystal_Small_A", tipAngle = -145f },
            new HardbloodShardVariant { texturePath = "Hardblood/Crystal_Small_B", tipAngle = -96f },
            new HardbloodShardVariant { texturePath = "Hardblood/Crystal_Small_C", tipAngle = -53f },
            new HardbloodShardVariant { texturePath = "Hardblood/Crystal_Small_D", tipAngle = -26f },
            new HardbloodShardVariant { texturePath = "Hardblood/Crystal_Small_E", tipAngle = 14f },
            new HardbloodShardVariant { texturePath = "Hardblood/Crystal_Small_F", tipAngle = 16f }
        };
        public float shardDrawSize = 1.2f;
        public float shardTrailLength = 1.4f;                                          // cells of streak behind each shard
        public float shardTrailWidth = 0.1f;
        public Color shardTrailColor = new Color(0.6f, 0.03f, 0.06f, 0.8f);
    }

    public class MimickedWeapon : IExposable
    {
        public ThingDef weaponDef;
        public ThingDef stuff;

        public string Label => GenLabel.ThingLabel(weaponDef, stuff).CapitalizeFirst();

        public bool Matches(Thing weapon) => weapon != null && weapon.def == weaponDef && weapon.Stuff == stuff;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Defs.Look(ref stuff, "stuff");
        }
    }
    public class HardbloodShardVariant
    {
        public string texturePath;
        public float tipAngle;   // which way the shard's point faces in its texture, degrees counterclockwise from east

        private Material material;

        public Material Material => material ?? (material = MaterialPool.MatFrom(texturePath, ShaderDatabase.Transparent));
    }

    [StaticConstructorOnStartup]
    public class Command_HardbloodWeaponry : Command_Action
    {
        private static readonly Texture2D AbilityBackground = ContentFinder<Texture2D>.Get("UI/Widgets/AbilityButBG");
        private static readonly Texture2D AbilityBackgroundShrunk = ContentFinder<Texture2D>.Get("UI/Widgets/AbilityButBGShrunk");

        public override Texture2D BGTexture => AbilityBackground;

        public override Texture2D BGTextureShrunk => AbilityBackgroundShrunk;
    }

    public class HardbloodWeaponry : IExposable
    {
        public List<MimickedWeapon> repertoire = new List<MimickedWeapon>();

        private static HardbloodWeaponryExtension cachedSettings;
        private static Texture2D cachedIcon;

        public static HardbloodWeaponryExtension Settings
        {
            get
            {
                if (cachedSettings == null) cachedSettings = DefDatabase<GeneDef>.GetNamed("Bloodfiends_Bloodfiend").GetModExtension<HardbloodWeaponryExtension>() ?? new HardbloodWeaponryExtension();
                return cachedSettings;
            }
        }

        private static Texture2D Icon
        {
            get
            {
                if (cachedIcon == null) cachedIcon = ContentFinder<Texture2D>.Get("Icons/HardbloodWeaponry", false) ?? BaseContent.BadTex;
                return cachedIcon;
            }
        }

        // ----- numbers -----

        public static int GenerationOf(Pawn pawn)
        {
            return pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node?.generationNumber ?? 7;
        }

        private static int CraftingOf(Pawn pawn) => pawn.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;

        public static int SlotsFor(Pawn pawn)
        {
            int crafting = CraftingOf(pawn);
            return Mathf.RoundToInt(Settings.slotsByGeneration.Evaluate(GenerationOf(pawn))) + Settings.extraSlotCraftingLevels.Count(level => crafting >= level);
        }

        /// <summary>
        /// What manifesting a weapon costs, as a share of hemogen (0.1 = 10 hemogen).
        /// </summary>
        public static float CostOf(Pawn pawn, MimickedWeapon mimicked)
        {
            HardbloodLanceExtension lance = mimicked.weaponDef.GetModExtension<HardbloodLanceExtension>();
            if (lance != null) return lance.manifestCost;
            float marketValue = mimicked.weaponDef.GetStatValueAbstract(StatDefOf.MarketValue, StuffFor(mimicked));
            return Settings.hemogenCostByMarketValue.Evaluate(marketValue) * Settings.costMultiplierByGeneration.Evaluate(GenerationOf(pawn)) / 100f;
        }

        /// <summary>
        /// What holding a replica drains per day, as a share of hemogen.
        /// </summary>
        public static float UpkeepPerDay(Pawn pawn) => Settings.upkeepPerDay * Settings.costMultiplierByGeneration.Evaluate(GenerationOf(pawn)) / 100f;

        public static QualityCategory QualityFor(Pawn pawn)
        {
            int quality = Mathf.RoundToInt(Settings.qualityByGeneration.Evaluate(GenerationOf(pawn))) + CraftingOf(pawn) / Mathf.Max(1, Settings.craftingLevelsPerQualityStep);
            return (QualityCategory)Mathf.Clamp(quality, (int)QualityCategory.Awful, (int)Settings.maximumQuality);
        }

        private static ThingDef StuffFor(MimickedWeapon mimicked)
        {
            if (!mimicked.weaponDef.MadeFromStuff) return null;
            return mimicked.stuff ?? GenStuff.DefaultStuffFor(mimicked.weaponDef);
        }

        private static float HemogenOf(Pawn pawn) => pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>()?.Value ?? 0f;

        // ----- gizmo -----

        public Gizmo GetGizmo(Pawn pawn)
        {
            var command = new Command_HardbloodWeaponry
            {
                defaultLabel = "Bloodfiends_HardbloodWeaponry".Translate(),
                defaultDesc = "Bloodfiends_HardbloodWeaponryDesc".Translate(repertoire.Count, SlotsFor(pawn)),
                icon = Icon,
                action = () => OpenMenu(pawn),
                Order = OrderAfterAbilities(pawn)
            };
            if (pawn.Downed) command.Disable("Bloodfiends_WeaponryDowned".Translate(pawn.LabelShort));
            return command;
        }

        /// <summary>
        /// Just past the pawn's last ability button, so it's drawn at the end of their abilities.
        /// </summary>
        private static float OrderAfterAbilities(Pawn pawn)
        {
            float lastAbilityOrder = 5f;
            List<Ability> abilities = pawn.abilities?.AllAbilitiesForReading;
            if (abilities != null) foreach (Ability ability in abilities) lastAbilityOrder = Mathf.Max(lastAbilityOrder, AbilityOrder(ability.def));
            return lastAbilityOrder + 0.00001f;
        }

        /// <summary>
        /// The order vanilla gives an ability's button (Command_Ability's constructor).
        /// </summary>
        private static float AbilityOrder(AbilityDef def) => 5f + (def.category?.displayOrder ?? 0) / 100f + def.displayOrder / 1000f + def.level / 10000f;

        private void OpenMenu(Pawn pawn)
        {
            var options = new List<FloatMenuOption>();
            GameComponent_HardbloodReplicas replicas = GameComponent_HardbloodReplicas.Instance;
            ThingWithComps held = pawn.equipment?.Primary;
            bool holdingReplica = held != null && replicas != null && replicas.IsReplica(held);

            if (held != null && !holdingReplica)
            {
                string label = "Bloodfiends_MimicWeapon".Translate(held.LabelShortCap);
                if (!HardbloodGuns.CanMimic(held, out string cannotMimicReason)) options.Add(new FloatMenuOption(label + " (" + cannotMimicReason + ")", null));
                else if (repertoire.Any(mimicked => mimicked.Matches(held))) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_AlreadyMimicked".Translate() + ")", null));
                else if (repertoire.Count >= SlotsFor(pawn)) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_RepertoireFull".Translate() + ")", null));
                else options.Add(new FloatMenuOption(label, () => Mimic(held)));
            }

            float hemogen = HemogenOf(pawn);
            AddLanceOption(pawn, options, held, holdingReplica, hemogen);
            foreach (MimickedWeapon mimicked in repertoire)
            {
                float cost = CostOf(pawn, mimicked);
                string label = "Bloodfiends_ManifestWeapon".Translate(mimicked.Label, (cost * 100f).ToString("0"));
                if (holdingReplica && mimicked.Matches(held)) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_AlreadyManifested".Translate() + ")", null));
                else if (hemogen < cost) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_NotEnoughHemogen".Translate() + ")", null));
                else options.Add(new FloatMenuOption(label, () => Manifest(pawn, mimicked)));
            }

            if (holdingReplica) options.Add(new FloatMenuOption("Bloodfiends_DissolveReplica".Translate(held.LabelShortCap), () => replicas.Dissolve(held)));
            foreach (MimickedWeapon mimicked in repertoire) options.Add(new FloatMenuOption("Bloodfiends_ForgetWeapon".Translate(mimicked.Label), () => repertoire.Remove(mimicked)));

            if (options.Count == 0) options.Add(new FloatMenuOption("Bloodfiends_NothingToMimic".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ----- actions -----

        public void Mimic(Thing weapon)
        {
            repertoire.Add(new MimickedWeapon { weaponDef = weapon.def, stuff = weapon.Stuff });
        }

        private void AddLanceOption(Pawn pawn, List<FloatMenuOption> options, ThingWithComps held, bool holdingReplica, float hemogen)
        {
            ThingDef lanceDef = BloodfiendsDefOf.Bloodfiends_HardbloodLance;
            HardbloodLanceExtension lance = lanceDef?.GetModExtension<HardbloodLanceExtension>();
            if (lance == null || GenerationOf(pawn) > lance.maximumGeneration) return;
            string label = "Bloodfiends_ManifestLance".Translate((lance.manifestCost * 100f).ToString("0"));
            if (holdingReplica && held.def == lanceDef) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_AlreadyManifested".Translate() + ")", null));
            else if (hemogen < lance.manifestCost) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_NotEnoughHemogen".Translate() + ")", null));
            else options.Add(new FloatMenuOption(label, () => ManifestLance(pawn)));
        }

        public void ManifestLance(Pawn pawn)
        {
            ThingDef lanceDef = BloodfiendsDefOf.Bloodfiends_HardbloodLance;
            var lance = (ThingWithComps)ThingMaker.MakeThing(lanceDef);
            Equip(pawn, lance, lanceDef.GetModExtension<HardbloodLanceExtension>().manifestCost);
        }

        public void Manifest(Pawn pawn, MimickedWeapon mimicked)
        {
            var replica = (ThingWithComps)ThingMaker.MakeThing(mimicked.weaponDef, StuffFor(mimicked));
            replica.TryGetComp<CompQuality>()?.SetQuality(QualityFor(pawn), ArtGenerationContext.Colony);
            Equip(pawn, replica, CostOf(pawn, mimicked));
        }

        private static void Equip(Pawn pawn, ThingWithComps replica, float cost)
        {
            GameComponent_HardbloodReplicas replicas = GameComponent_HardbloodReplicas.Instance;
            if (replicas == null || pawn.equipment == null || HemogenOf(pawn) < cost) return;

            ThingWithComps held = pawn.equipment.Primary;
            if (held != null)
            {
                if (replicas.IsReplica(held)) replicas.Dissolve(held);
                else if (!pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer)) return;
            }

            // registered before it's equipped, so its first frame is already drawn as hardblood
            replicas.Register(replica, true);
            pawn.equipment.AddEquipment(replica);
            GeneUtility.OffsetHemogen(pawn, -cost);
            replicas.StartForming(replica, pawn);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref repertoire, "repertoire", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (repertoire == null) repertoire = new List<MimickedWeapon>();
                repertoire.RemoveAll(mimicked => mimicked?.weaponDef == null);   // weapons from mods that were removed
            }
            repertoire.RemoveAll(mimicked => mimicked?.weaponDef == null || !HardbloodGuns.IsPlainWeaponDef(mimicked.weaponDef));
        }
    }
}
