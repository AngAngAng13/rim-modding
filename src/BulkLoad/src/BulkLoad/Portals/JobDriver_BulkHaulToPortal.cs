using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public class JobDriver_BulkHaulToPortal : JobDriver_BulkLoadBase
    {
        private MapPortal Portal => job.GetTarget(TargetIndex.B).Thing as MapPortal;

        protected override Thing DestinationThing => Portal;

        protected override bool HasMoreToLoad =>
            Portal != null && Portal.LoadInProgress;

        protected override ThingOwner GetDestinationContainer() =>
            Portal?.GetDirectlyHeldThings();

        protected override ThingCount FindFirstThingToLoad() =>
            BulkLoadServices.Planner.FindThingToLoad(pawn, Portal?.leftToLoad, loadDestination: DestinationThing);

        protected override List<TransferableOneWay> GetLeftToLoad() =>
            Portal?.leftToLoad;
    }
}
