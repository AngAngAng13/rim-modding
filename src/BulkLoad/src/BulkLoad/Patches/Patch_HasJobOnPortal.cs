using HarmonyLib;
using RimWorld;
using Verse;

namespace BulkLoad
{
    [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.HasJobOnPortal))]
    public static class Patch_HasJobOnPortal
    {
        static bool Prefix(Pawn pawn, MapPortal portal, ref bool __result)
        {
            if (!BulkLoadToggles.PortalsEnabled)
                return true;

            if (portal == null
                || portal.leftToLoad.NullOrEmpty()
                || !BulkLoadAssignmentPolicy.IsEligibleHauler(pawn, portal))
            {
                __result = false;
                return false;
            }

            return BulkLoadServices.OfferGate.CheckHasJob(pawn, new PortalBulkSource(portal), ref __result);
        }
    }
}
