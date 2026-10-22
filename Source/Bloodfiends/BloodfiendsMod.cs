using HarmonyLib;
using SingularityFramework.WeaponAnchor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends
{
    public class BloodfiendsMod : Mod
    {
        public BloodfiendsMod(ModContentPack content) : base(content)
        {
            new Harmony("shapto.bloodfiends").PatchAll();
            WeaponOverlays.Register(Bloodfiends.Hardblood.HardbloodGraphics.BloodbagCoating);
        }
    }
}
