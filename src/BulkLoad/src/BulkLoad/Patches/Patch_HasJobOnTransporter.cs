using HarmonyLib;
using RimWorld;
using Verse;

namespace BulkLoad
{
    [HarmonyPatch(typeof(LoadTransportersJobUtility), nameof(LoadTransportersJobUtility.HasJobOnTransporter))]
    public static class Patch_HasJobOnTransporter
    {
        static bool Prefix(Pawn pawn, CompTransporter transporter, ref bool __result)
        {
            if (!BulkLoadToggles.TransportersEnabled)
                return true;

            if (transporter == null
                || !transporter.AnythingLeftToLoad
                || !BulkLoadAssignmentPolicy.IsEligibleHauler(pawn, transporter.parent))
            {
                __result = false;
                return false;
            }

            return BulkLoadServices.OfferGate.CheckHasJob(pawn, new TransporterBulkSource(transporter), ref __result);
        }
    }
}
