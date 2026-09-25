namespace BulkLoad
{
    internal static class BulkLoadToggles
    {
        public static bool MasterEnabled => BulkLoadMod.Settings == null || BulkLoadMod.Settings.masterEnabled;

        public static bool TransportersEnabled =>
            MasterEnabled && (BulkLoadMod.Settings == null || BulkLoadMod.Settings.enableTransporters);

        public static bool PortalsEnabled =>
            MasterEnabled && (BulkLoadMod.Settings == null || BulkLoadMod.Settings.enablePortals);

        public static bool CarriersEnabled =>
            MasterEnabled && (BulkLoadMod.Settings == null || BulkLoadMod.Settings.enableCarriers);

        public static bool BillsEnabled =>
            MasterEnabled && (BulkLoadMod.Settings == null || BulkLoadMod.Settings.enableBills);

        public static bool SelfUnloadRescueEnabled =>
            MasterEnabled && (BulkLoadMod.Settings == null || BulkLoadMod.Settings.enableSelfUnloadRescue);
    }
}
