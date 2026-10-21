using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Family
{
    public class BloodfiendStartingTierExtension : DefModExtension
    {
        public List<StartingTierWeight> tierWeights = new List<StartingTierWeight>();
        public float joinPlayerFamilyChance = 0.1f;
    }

    public class StartingTierWeight
    {
        public int tier;
        public float weight;
    }
}
