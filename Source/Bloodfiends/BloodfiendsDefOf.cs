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
        static BloodfiendsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BloodfiendsDefOf));
    }
}
