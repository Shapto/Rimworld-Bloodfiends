using Bloodfiends.Family;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.SharedThirst
{
    public class SharedThirstExtension : DefModExtension
    {
        public SimpleCurve drainMultiplierByOtherFamilyMembers;
    }

    public class Gene_SharedThirst : Gene_HemogenDrain
    {
        public int OtherFamilyMemberCount
        {
            get
            {
                BloodfiendFamilyNode node = pawn.health.hediffSet.GetFirstHediff<Hediff_BloodfiendLineage>()?.Node;
                return node == null ? 0 : GameComponent_BloodfiendFamilies.Instance?.GetOtherFamilyMemberCount(node.familyId) ?? 0;
            }
        }

        public float DrainMultiplier => def.GetModExtension<SharedThirstExtension>()?.drainMultiplierByOtherFamilyMembers?.Evaluate(OtherFamilyMemberCount) ?? 1f;
    }

    [HarmonyPatch(typeof(Gene_HemogenDrain), nameof(Gene_HemogenDrain.ResourceLossPerDay), MethodType.Getter)]
    public static class Patch_GeneHemogenDrain_ResourceLossPerDay
    {
        public static void Postfix(Gene_HemogenDrain __instance, ref float __result)
        {
            if (__instance is Gene_SharedThirst sharedThirst) __result *= sharedThirst.DrainMultiplier;
        }
    }
}
