using Verse;

namespace Bloodfiends.Hardblood
{
    /// <summary>
    /// A hardblood lance named after the Bloodfiend whose blood formed it: La Sangre de Sancho, La Sangre de Thomas.
    /// Falls back to the plain def label if it somehow has no owner.
    /// </summary>
    public class Thing_HardbloodLance : ThingWithComps
    {
        public string ownerName;

        public override string LabelNoCount => ownerName.NullOrEmpty() ? base.LabelNoCount : "Bloodfiends_LanceLabel".Translate(ownerName.Named("OWNER")).Resolve();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ownerName, "ownerName");
        }
    }
}