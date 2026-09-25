using RimWorld;
using Verse;

namespace BulkLoad
{
    [DefOf]
    public static class BulkLoadDefOf
    {
        public static JobDef BulkHaulToTransporter;
        public static JobDef BulkHaulToPortal;

        public static JobDef BulkUnloadCarrier;
        public static JobDef BulkDoBill;
        public static JobDef BulkUnloadOwnInventory;

        static BulkLoadDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BulkLoadDefOf));
        }
    }
}
