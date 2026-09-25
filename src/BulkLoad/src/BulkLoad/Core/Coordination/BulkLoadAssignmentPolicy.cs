using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    internal static class BulkLoadAssignmentPolicy
    {
        public const float ClusterRadius = 14f;
        public const float ReservationEncumbrance = 0.95f;

        public static bool IsEligibleHauler(Pawn pawn, Thing destination)
        {
            if (pawn == null
                || destination == null
                || !pawn.Spawned
                || pawn.Dead
                || pawn.Downed
                || pawn.Drafted
                || !IsAvailableForAssignment(pawn)
                || pawn.Faction != Faction.OfPlayerSilentFail
                || pawn.carryTracker?.CarriedThing != null
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                || pawn.WorkTagIsDisabled(WorkTags.Hauling)
                || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
            {
                return false;
            }

            return pawn.CanReach(destination, PathEndMode.Touch, pawn.NormalMaxDanger());
        }

        private static bool IsAvailableForAssignment(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            if (job == null)
                return true;
            if (job.playerForced)
                return false;

            JobDef def = job.def;
            return def == JobDefOf.Wait
                || def == JobDefOf.Wait_Combat
                || def == JobDefOf.Wait_Wander
                || def == JobDefOf.Wait_MaintainPosture
                || def == JobDefOf.GotoWander;
        }

    }
}
