using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.SharedThirst
{
    public class Hediff_Feasting : HediffWithComps
    {
        public override bool Visible => CurStageIndex > 0;
    }
}
