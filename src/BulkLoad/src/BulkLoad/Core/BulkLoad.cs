using HarmonyLib;
using Verse;

namespace BulkLoad
{
    [StaticConstructorOnStartup]
    public static class BulkLoad
    {
        static BulkLoad()
        {
            BulkLoadServices.EnsureInitialized();
            var harmony = new Harmony("pineappleLemonade67.bulkload");
            harmony.PatchAll();
            BulkLoadDiagnostics.Info(
                "startup",
                "Initialized: transporters, portals, carrier unload, and bulk bills.");
        }
    }
}
