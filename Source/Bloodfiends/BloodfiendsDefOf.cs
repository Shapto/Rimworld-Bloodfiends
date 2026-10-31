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
        public static SoundDef Bloodfiends_HardbloodForming;
        public static SoundDef Bloodfiends_HardbloodHit;
        public static SoundDef Bloodfiends_HardbloodShatter;
        public static SoundDef Bloodfiends_LanceForming;
        public static SoundDef Bloodfiends_LanceChargeWindup;
        public static SoundDef Bloodfiends_LanceChargeHit;
        public static HediffDef Bloodfiends_SeekingEmbrace;
        public static AbilityDef Bloodfiends_Embrace;
        public static JobDef Bloodfiends_ReceiveEmbrace;
        public static SitePartDef Bloodfiends_ElderLair;
        static BloodfiendsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BloodfiendsDefOf));
    }
}
