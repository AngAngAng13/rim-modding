using Verse;

namespace BulkLoad
{
    public class BulkLoadSettings : ModSettings
    {
        public bool masterEnabled = true;
        public bool enableTransporters = true;
        public bool enablePortals = true;
        public bool enableCarriers = true;
        public bool enableBills = true;
        public bool enableSelfUnloadRescue = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref masterEnabled, "masterEnabled", true);
            Scribe_Values.Look(ref enableTransporters, "enableTransporters", true);
            Scribe_Values.Look(ref enablePortals, "enablePortals", true);
            Scribe_Values.Look(ref enableCarriers, "enableCarriers", true);
            Scribe_Values.Look(ref enableBills, "enableBills", true);
            Scribe_Values.Look(ref enableSelfUnloadRescue, "enableSelfUnloadRescue", true);
        }
    }
}
