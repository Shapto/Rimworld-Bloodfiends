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
        static BloodfiendsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BloodfiendsDefOf));
    }
}
