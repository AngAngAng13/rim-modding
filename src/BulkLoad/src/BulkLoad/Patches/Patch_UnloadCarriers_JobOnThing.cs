using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    [HarmonyPatch(typeof(WorkGiver_UnloadCarriers), nameof(WorkGiver_UnloadCarriers.JobOnThing))]
    public static class Patch_UnloadCarriers_JobOnThing
    {
        static bool Prefix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (!BulkLoadToggles.CarriersEnabled)
                return true;

            if (pawn.Faction != Faction.OfPlayerSilentFail)
                return true;

            if (pawn.carryTracker.CarriedThing != null)
                return true;

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return true;

            if (!(t is Pawn carrier) || carrier == pawn)
                return true;

            if (carrier.inventory == null || carrier.inventory.innerContainer.Count == 0)
                return true;



            __result = JobMaker.MakeJob(BulkLoadDefOf.BulkUnloadCarrier, t);
            __result.ignoreForbidden = true;
            BulkLoadDiagnostics.Debug(
                "carriers",
                "Selected bulk carrier unload for " + pawn.LabelShort + " carrier=" + carrier.LabelShort);
            BulkLoadDiagnostics.RecordModAction(
                pawn,
                "job_selected",
                "job=BulkUnloadCarrier carrier=" + carrier.LabelShort);
            return false;
        }
    }
}
