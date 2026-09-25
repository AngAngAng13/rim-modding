using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    [HarmonyPatch(typeof(LoadTransportersJobUtility), nameof(LoadTransportersJobUtility.JobOnTransporter))]
    public static class Patch_JobOnTransporter
    {
        static bool Prefix(Pawn p, CompTransporter transporter, ref Job __result)
        {
            if (!BulkLoadToggles.TransportersEnabled)
                return true;

            if (p.Faction != Faction.OfPlayerSilentFail)
                return true;

            if (transporter == null || !transporter.AnythingLeftToLoad)
                return true;

            if (!BulkLoadAssignmentPolicy.IsEligibleHauler(p, transporter.parent))
                return true;

            return BulkLoadServices.OfferGate.CheckJobOn(p, new TransporterBulkSource(transporter), ref __result);
        }
    }
}
