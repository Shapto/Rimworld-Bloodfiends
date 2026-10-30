using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Bloodfiends.Hardblood
{
    /// <summary>
    /// What hardblood can and can't copy, and how replica guns run on blood. A replica copies a weapon's shape, not what
    /// makes it special: only weapons with nothing but the ordinary weapon parts can be mimicked (no unique, persona or
    /// relic weapons, nothing with abilities or charges). Replica guns fire hardblood shards, and every shot costs
    /// hemogen in proportion to its damage, so a minigun drinks far more than a rifle.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HardbloodGuns
    {
        /// <summary>
        /// The parts every plain vanilla weapon has. Anything else on a weapon (an ability, a persona, unique traits,
        /// charges) means hardblood can't copy it. More can be allowed in XML for mods that add a part to every weapon.
        /// </summary>
        private static readonly HashSet<Type> PlainCompClasses = new HashSet<Type> { typeof(CompForbiddable), typeof(CompEquippable), typeof(CompQuality), typeof(CompArt), typeof(CompStyleable), typeof(CompBiocodable) };

        private static HashSet<Type> allowedCompClasses;
        private static readonly HashSet<Projectile> shards = new HashSet<Projectile>();
        private static Material shardMaterial;

        public static bool firingReplica;

        private static HashSet<Type> AllowedCompClasses
        {
            get
            {
                if (allowedCompClasses != null) return allowedCompClasses;
                allowedCompClasses = new HashSet<Type>(PlainCompClasses);
                foreach (string typeName in HardbloodWeaponry.Settings.extraMimicableCompClasses)
                {
                    Type type = GenTypes.GetTypeInAnyAssembly(typeName);
                    if (type != null) allowedCompClasses.Add(type);
                }
                return allowedCompClasses;
            }
        }

        /// <summary>
        /// True if hardblood can copy this kind of weapon at all (its def has only plain weapon parts).
        /// </summary>
        public static bool IsPlainWeaponDef(ThingDef weaponDef)
        {
            if (weaponDef == null || weaponDef.GetModExtension<HardbloodLanceExtension>() != null) return false;
            return weaponDef.comps == null || weaponDef.comps.All(comp => comp.compClass != null && AllowedCompClasses.Contains(comp.compClass));
        }

        /// <summary>
        /// True if this particular weapon can be mimicked; if not, the reason to show.
        /// </summary>
        public static bool CanMimic(Thing weapon, out string reason)
        {
            reason = null;
            if (weapon.StyleSourcePrecept is Precept_Relic || !IsPlainWeaponDef(weapon.def))
            {
                reason = "Bloodfiends_CannotMimicSpecial".Translate();
                return false;
            }
            return true;
        }

        // ----- replica guns -----

        public static bool IsReplicaGun(Verb verb, out Pawn shooter)
        {
            shooter = verb.CasterPawn;
            ThingWithComps weapon = verb.EquipmentSource;
            return shooter != null && weapon != null && GameComponent_HardbloodReplicas.Instance?.IsReplica(weapon) == true;
        }

        /// <summary>
        /// What one shot costs, as a share of hemogen: its damage times the per-damage price.
        /// </summary>
        public static float ShotCost(Verb_LaunchProjectile verb)
        {
            ThingDef projectile = verb.GetProjectile();
            float damage = projectile?.projectile?.GetDamageAmount(verb.EquipmentSource) ?? 0f;
            return damage * HardbloodWeaponry.Settings.hemogenPerShotDamage / 100f;
        }

        public static bool CanAffordShot(Pawn shooter, Verb_LaunchProjectile verb)
        {
            Gene_Hemogen hemogenGene = shooter.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            return hemogenGene != null && hemogenGene.Value >= ShotCost(verb);
        }

        public static void MarkShard(Projectile projectile)
        {
            if (shards.Count > 500) shards.RemoveWhere(shard => shard.Destroyed);
            shards.Add(projectile);
        }

        /// <summary>
        /// Draws a hardblood shard in place of the bullet, if this projectile came from a replica. False to draw it
        /// normally.
        /// </summary>
        public static bool TryDrawShard(Projectile projectile, Vector3 drawLocation)
        {
            if (!shards.Contains(projectile)) return false;
            if (projectile.Destroyed)
            {
                shards.Remove(projectile);
                return false;
            }
            HardbloodWeaponryExtension settings = HardbloodWeaponry.Settings;
            if (shardMaterial == null) shardMaterial = MaterialPool.MatFrom(settings.shardTexturePath, ShaderDatabase.Transparent);
            Quaternion rotation = projectile.ExactRotation * Quaternion.AngleAxis(settings.shardAngleOffset, Vector3.up);
            Graphics.DrawMesh(MeshPool.GridPlane(new Vector2(settings.shardDrawSize, settings.shardDrawSize)), drawLocation, rotation, shardMaterial, 0);
            return true;
        }
    }

    /// <summary>
    /// Each shot from a replica gun is paid for in blood; a Bloodfiend who can't pay can't fire.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
    public static class Patch_VerbLaunchProjectile_TryCastShot_Hardblood
    {
        public static bool Prefix(Verb_LaunchProjectile __instance, ref bool __result, ref bool __state)
        {
            __state = false;
            if (!HardbloodGuns.IsReplicaGun(__instance, out Pawn shooter)) return true;
            if (!HardbloodGuns.CanAffordShot(shooter, __instance))
            {
                __result = false;
                return false;
            }
            GeneUtility.OffsetHemogen(shooter, -HardbloodGuns.ShotCost(__instance));
            HardbloodGuns.firingReplica = true;
            __state = true;
            return true;
        }

        public static void Finalizer(bool __state)
        {
            if (__state) HardbloodGuns.firingReplica = false;
        }
    }

    /// <summary>
    /// A replica gun with too little blood for a shot can't be used, so pawns don't stand there trying.
    /// </summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectile), nameof(Verb_LaunchProjectile.Available))]
    public static class Patch_VerbLaunchProjectile_Available_Hardblood
    {
        public static void Postfix(Verb_LaunchProjectile __instance, ref bool __result)
        {
            if (__result && HardbloodGuns.IsReplicaGun(__instance, out Pawn shooter) && !HardbloodGuns.CanAffordShot(shooter, __instance)) __result = false;
        }
    }

    /// <summary>
    /// Projectiles launched while a replica gun is firing are hardblood shards.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Projectile_Launch_Hardblood
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(Projectile)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "Launch");
        }

        public static void Postfix(Projectile __instance)
        {
            if (HardbloodGuns.firingReplica) HardbloodGuns.MarkShard(__instance);
        }
    }

    [HarmonyPatch(typeof(Projectile), "DrawAt")]
    public static class Patch_Projectile_DrawAt_Hardblood
    {
        public static bool Prefix(Projectile __instance, Vector3 drawLoc) => !HardbloodGuns.TryDrawShard(__instance, drawLoc);
    }
}