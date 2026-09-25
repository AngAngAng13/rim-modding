using UnityEngine;
using Verse;

namespace RaidFlow
{
    public class RaidFlowMod : Mod
    {
        public const string HarmonyId = "lemonade.raidflow";

        public static RaidFlowSettings Settings;

        public RaidFlowMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RaidFlowSettings>();
        }

        public override string SettingsCategory()
        {
            return "Raid Flow";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            bool useShared = Settings == null || Settings.useSharedRoutes;
            listing.CheckboxLabeled("Use shared route maps for raiders", ref useShared);
            if (Settings != null)
                Settings.useSharedRoutes = useShared;
            if (listing.ButtonText("Reset timing stats"))
                RaidFlowProfiler.Reset();
            int count;
            double typical;
            double slowest;
            if (RaidFlowProfiler.TryGetStats(true, out count, out typical, out slowest))
                listing.Label("Shared routes: " + count + " paths, typical " + typical.ToString("F2") + " ms, slowest 1% " + slowest.ToString("F2") + " ms.");
            else
                listing.Label("Shared routes: no raider paths measured yet.");
            if (RaidFlowProfiler.TryGetStats(false, out count, out typical, out slowest))
                listing.Label("Normal pathing: " + count + " paths, typical " + typical.ToString("F2") + " ms, slowest 1% " + slowest.ToString("F2") + " ms.");
            else
                listing.Label("Normal pathing: no raider paths measured yet.");
            listing.Label("Turn the toggle off and keep playing to measure normal pathing on the same raids.");
            listing.End();
        }
    }
}
