using RimWorld;
using SingularityFramework.Kitbash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends
{
    [DefOf]
    public static class BloodfiendsDefOf
    {
        public static HediffDef Bloodfiends_Lineage;
        public static XenotypeDef Bloodfiends_Bloodfiend;
        public static AbilityDef Bloodfiends_CoagulateAndMeltAway;
        public static HediffDef Bloodfiends_Feasting;
        public static ThoughtDef Bloodfiends_FamilialImpiety;
        public static HediffDef Bloodfiends_HardbloodGrowths;
        public static KitbashPieceSetDef Bloodfiends_HardbloodCrystals;
        public static HediffDef Bloodfiends_HardbloodShell;
        public static XenotypeDef Bloodfiends_Bloodbag;
        public static HediffDef Bloodfiends_BloodbagInfection;
        public static AbilityDef Bloodfiends_Coagulate;
        public static AbilityDef Bloodfiends_Mantle;
        public static AbilityDef Bloodfiends_LaSangre;
        public static ThingDef Bloodfiends_LaSangreFlight;
        public static ThoughtDef Bloodfiends_ChastisedByElder;
        public static ThoughtDef Bloodfiends_SiblingQuarrel;
        public static ThoughtDef Bloodfiends_StruckByLesserKindred;
        public static ThoughtDef Bloodfiends_KilledElder;
        public static ThingDef Bloodfiends_HardbloodLance;
        public static JobDef Bloodfiends_HardbloodLanceCharge;
        public static JobDef Bloodfiends_HardbloodLanceThrust;
        static BloodfiendsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BloodfiendsDefOf));
    }
}
