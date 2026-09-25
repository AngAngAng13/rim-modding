using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.JobOnPortal))]
    public static class Patch_JobOnPortal
    {
        static bool Prefix(Pawn p, MapPortal portal, ref Job __result)
        {
            if (!BulkLoadToggles.PortalsEnabled)
                return true;

            if (p.Faction != Faction.OfPlayerSilentFail)
                return true;

            if (portal == null || portal.leftToLoad.NullOrEmpty())
                return true;

            if (!BulkLoadAssignmentPolicy.IsEligibleHauler(p, portal))
                return true;

            return BulkLoadServices.OfferGate.CheckJobOn(p, new PortalBulkSource(portal), ref __result);
        }
    }
}
