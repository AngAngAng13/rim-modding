using UnityEngine;
using Verse;

namespace BulkLoad
{
    public class BulkLoadMod : Mod
    {
        public static BulkLoadSettings Settings;

        public BulkLoadMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BulkLoadSettings>();
        }

        public override string SettingsCategory()
        {
            return "Bulk Load";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Enable Bulk Load", ref Settings.masterEnabled);
            if (!Settings.masterEnabled)
                listing.Label("Master switch is off: pawns use vanilla jobs. In-flight bulk jobs finish, no new ones start.");
            listing.Gap();
            listing.CheckboxLabeled("Bulk load transporters (pods, shuttles)", ref Settings.enableTransporters);
            listing.CheckboxLabeled("Bulk load portals (Anomaly pit gates)", ref Settings.enablePortals);
            listing.CheckboxLabeled("Unload carrier animals in one pass", ref Settings.enableCarriers);
            listing.CheckboxLabeled("Bulk crafting bills (all ingredients in one trip)", ref Settings.enableBills);
            listing.CheckboxLabeled(
                "Unload leftover goods",
                ref Settings.enableSelfUnloadRescue,
                "if you use Combat Extended and notice guys coming home from a caravan holding onto stuff instead of putting it away, this might help. it may also be worth checking whether they have a CE loadout assigned, since pawns with one tend to unload on their own.");
            listing.Gap();
            listing.Label("Safe removal: turn the master switch off, wait for active bulk jobs to finish, save, then remove the mod.");
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
