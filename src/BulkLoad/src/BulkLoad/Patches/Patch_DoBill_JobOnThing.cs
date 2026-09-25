using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class Patch_DoBill_JobOnThing
    {
        static void Postfix(Pawn pawn, ref Job __result)
        {
            if (!BulkLoadToggles.BillsEnabled)
                return;
            if (__result == null)
                return;
            if (__result.def != JobDefOf.DoBill)
                return;
            if (pawn.Faction != Faction.OfPlayerSilentFail)
                return;
            if (!pawn.RaceProps.Humanlike)
                return;
            if (pawn.carryTracker.CarriedThing != null)
                return;
            if (__result.targetQueueB == null || __result.targetQueueB.Count < 2)
                return;
            foreach (LocalTargetInfo queued in __result.targetQueueB)
            {
                Thing ingredient = queued.Thing;
                if (ingredient == null || !ingredient.Spawned)
                    return;
            }

            if (__result.GetTarget(TargetIndex.A).Thing is Building_WorkTableAutonomous)
                return;

            __result.def = BulkLoadDefOf.BulkDoBill;
            BulkLoadDiagnostics.Debug(
                "bills",
                "Selected bulk bill job for " + pawn.LabelShort + " ingredients=" + __result.targetQueueB.Count);
            BulkLoadDiagnostics.RecordModAction(
                pawn,
                "job_selected",
                "job=BulkDoBill ingredients=" + __result.targetQueueB.Count);
        }
    }
}
