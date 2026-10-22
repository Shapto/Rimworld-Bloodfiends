using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Hardblood
{
    public class GameComponent_HardbloodCoatings : GameComponent
    {
        private HashSet<int> coatedWeaponIds = new HashSet<int>();

        public static GameComponent_HardbloodCoatings Instance => Current.Game?.GetComponent<GameComponent_HardbloodCoatings>();

        public GameComponent_HardbloodCoatings(Game game)
        {
        }

        public bool MarkCoated(Thing weapon)
        {
            return coatedWeaponIds.Add(weapon.thingIDNumber);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref coatedWeaponIds, "coatedWeaponIds", LookMode.Value);
            if (coatedWeaponIds == null) coatedWeaponIds = new HashSet<int>();
        }
    }
}
