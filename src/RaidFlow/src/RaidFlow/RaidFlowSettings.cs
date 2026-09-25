using Verse;

namespace RaidFlow
{
    public class RaidFlowSettings : ModSettings
    {
        public bool useSharedRoutes = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useSharedRoutes, "useSharedRoutes", true);
        }
    }
}
