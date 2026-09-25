using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    internal interface ILoadingOfferGate
    {
        bool CheckHasJob(Pawn pawn, BulkLoadDestinationSource source, ref bool result);
        bool CheckJobOn(Pawn pawn, BulkLoadDestinationSource source, ref Job result);
    }

    internal sealed class LoadingOfferGate : ILoadingOfferGate
    {
        private readonly ILoadingCoordinator coordinator;
        private readonly ILoadingPlanner planner;

        public LoadingOfferGate(ILoadingCoordinator coordinator, ILoadingPlanner planner)
        {
            this.coordinator = coordinator;
            this.planner = planner;
        }

        public bool CheckHasJob(Pawn pawn, BulkLoadDestinationSource source, ref bool result)
        {
            if (pawn.Faction != Faction.OfPlayerSilentFail)
                return true;
            if (!pawn.RaceProps.Humanlike)
                return true;

            if (planner.FindThingToLoad(
                pawn,
                source.LeftToLoad,
                null,
                logSelection: false,
                loadDestination: source.Destination).Thing == null)
            {
                coordinator.CancelOffer(pawn, source.Destination);
                if (coordinator.HasActiveJobFor(source.Destination))
                {
                    result = false;
                    return false;
                }
                BulkLoadDiagnostics.DebugThrottled(
                    "jobs",
                    "noload_" + pawn.thingIDNumber + "_" + (source.Destination?.thingIDNumber ?? -1),
                    600,
                    "No loadable for " + pawn.LabelShort + source.DescribeTarget()
                    + " " + coordinator.DescribeBlockingState(source.Destination));
                return true;
            }

            if (coordinator.ShouldOffer(pawn, source.Destination, candidate =>
                planner.FindThingToLoad(
                    candidate,
                    source.LeftToLoad,
                    null,
                    logSelection: false,
                    loadDestination: source.Destination).Thing != null))
            {
                result = true;
                return false;
            }

            BulkLoadDiagnostics.DebugThrottled(
                "jobs",
                "deny_" + pawn.thingIDNumber + "_" + (source.Destination?.thingIDNumber ?? -1),
                600,
                "Turn denied for " + pawn.LabelShort + source.DescribeTarget()
                + " " + coordinator.DescribeBlockingState(source.Destination));
            result = false;
            return false;
        }

        public bool CheckJobOn(Pawn pawn, BulkLoadDestinationSource source, ref Job result)
        {
            if (pawn.Faction != Faction.OfPlayerSilentFail)
                return true;
            if (!pawn.RaceProps.Humanlike)
                return true;

            var thingCount = planner.FindThingToLoad(
                pawn,
                source.LeftToLoad,
                loadDestination: source.Destination);
            if (thingCount.Thing == null || thingCount.Thing is Pawn)
            {
                coordinator.CancelOffer(pawn, source.Destination);
                return true;
            }

            if (!coordinator.TryClaimOffer(pawn, source.Destination))
            {
                result = null;
                return false;
            }

            result = source.MakeBulkJob(pawn);
            result.ignoreForbidden = true;
            BulkLoadDiagnostics.Debug(
                source.LogChannel,
                "Selected bulk job " + source.JobLabel + " for " + pawn.LabelShort + source.DescribeTarget());
            BulkLoadDiagnostics.RecordModAction(
                pawn,
                "job_selected",
                "job=" + source.JobLabel + source.DescribeTarget());
            return false;
        }
    }
}
