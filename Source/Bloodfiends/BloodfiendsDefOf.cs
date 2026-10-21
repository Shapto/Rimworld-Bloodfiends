using RimWorld;
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
        static BloodfiendsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BloodfiendsDefOf));
    }
}
