using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public class JobDriver_BulkHaulToTransporter : JobDriver_BulkLoadBase
    {
        private Thing TransporterThing => job.GetTarget(TargetIndex.B).Thing;
        private CompTransporter Transporter => TransporterThing?.TryGetComp<CompTransporter>();

        protected override Thing DestinationThing => TransporterThing;

        protected override bool HasMoreToLoad =>
            Transporter != null && Transporter.AnythingLeftToLoad;

        protected override ThingOwner GetDestinationContainer() =>
            Transporter?.innerContainer;

        protected override ThingCount FindFirstThingToLoad() =>
            BulkLoadServices.Planner.FindThingToLoad(pawn, Transporter?.leftToLoad, loadDestination: DestinationThing);

        protected override List<TransferableOneWay> GetLeftToLoad() =>
            Transporter?.leftToLoad;
    }
}
