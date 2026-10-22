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
    /// <summary>
    /// Settings for Hardblood Weaponry, on the Bloodfiend gene: how many weapons a Bloodfiend can keep, what manifesting
    /// and holding a replica costs, and how good the replicas are. Hemogen is in the points shown in game (100 = a full
    /// default bar).
    /// </summary>
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
        public float formSeconds = 3f;
        public float lineWidth = 0.12f;
    }

    /// <summary>
    /// A weapon type a Bloodfiend has mimicked: what it is and what it was made of.
    /// </summary>
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

    /// <summary>
    /// A Bloodfiend's Hardblood Weaponry: the weapons they've mimicked, and the gizmo that mimics and manifests them.
    /// Saving a weapon only records its type; the real weapon is untouched. Manifesting makes a hardblood replica of it in
    /// their hands (the weapon they were holding goes into their inventory), which dissolves when it leaves their hands.
    /// </summary>
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
                if (cachedIcon == null) cachedIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/Bloodfiends_HardbloodWeaponry", false) ?? BaseContent.BadTex;
                return cachedIcon;
            }
        }

        // ----- numbers -----

        /// <summary>
        /// The Bloodfiend's Kindred tier (1 = First Kindred). Unknown counts as the weakest.
        /// </summary>
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
            var command = new Command_Action
            {
                defaultLabel = "Bloodfiends_HardbloodWeaponry".Translate(),
                defaultDesc = "Bloodfiends_HardbloodWeaponryDesc".Translate(repertoire.Count, SlotsFor(pawn)),
                icon = Icon,
                action = () => OpenMenu(pawn)
            };
            if (pawn.Downed) command.Disable("Bloodfiends_WeaponryDowned".Translate(pawn.LabelShort));
            return command;
        }

        private void OpenMenu(Pawn pawn)
        {
            var options = new List<FloatMenuOption>();
            GameComponent_HardbloodReplicas replicas = GameComponent_HardbloodReplicas.Instance;
            ThingWithComps held = pawn.equipment?.Primary;
            bool holdingReplica = held != null && replicas != null && replicas.IsReplica(held);

            if (held != null && !holdingReplica)
            {
                string label = "Bloodfiends_MimicWeapon".Translate(held.LabelShortCap);
                if (repertoire.Any(mimicked => mimicked.Matches(held))) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_AlreadyMimicked".Translate() + ")", null));
                else if (repertoire.Count >= SlotsFor(pawn)) options.Add(new FloatMenuOption(label + " (" + "Bloodfiends_RepertoireFull".Translate() + ")", null));
                else options.Add(new FloatMenuOption(label, () => Mimic(held)));
            }

            float hemogen = HemogenOf(pawn);
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

        /// <summary>
        /// Pays the cost, puts the held weapon away (or dissolves a held replica) and forms the replica in the
        /// Bloodfiend's hands.
        /// </summary>
        public void Manifest(Pawn pawn, MimickedWeapon mimicked)
        {
            GameComponent_HardbloodReplicas replicas = GameComponent_HardbloodReplicas.Instance;
            float cost = CostOf(pawn, mimicked);
            if (replicas == null || pawn.equipment == null || HemogenOf(pawn) < cost) return;

            ThingWithComps held = pawn.equipment.Primary;
            if (held != null)
            {
                if (replicas.IsReplica(held)) replicas.Dissolve(held);
                else if (!pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer)) return;
            }

            var replica = (ThingWithComps)ThingMaker.MakeThing(mimicked.weaponDef, StuffFor(mimicked));
            replica.TryGetComp<CompQuality>()?.SetQuality(QualityFor(pawn), ArtGenerationContext.Colony);
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
        }
    }
}
