using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    internal abstract class BulkLoadDestinationSource
    {
        public abstract Thing Destination { get; }
        public abstract List<TransferableOneWay> LeftToLoad { get; }
        public abstract string LogChannel { get; }
        public abstract string JobLabel { get; }
        public abstract Job MakeBulkJob(Pawn pawn);
        public abstract string DescribeTarget();
    }

    internal sealed class TransporterBulkSource : BulkLoadDestinationSource
    {
        private readonly CompTransporter transporter;

        public TransporterBulkSource(CompTransporter transporter)
        {
            this.transporter = transporter;
        }

        public override Thing Destination => transporter?.parent;
        public override List<TransferableOneWay> LeftToLoad => transporter?.leftToLoad;
        public override string LogChannel => "transporters";
        public override string JobLabel => "BulkHaulToTransporter";

        public override Job MakeBulkJob(Pawn pawn) =>
            JobMaker.MakeJob(BulkLoadDefOf.BulkHaulToTransporter, transporter.parent, transporter.parent);

        public override string DescribeTarget() =>
            " target=" + transporter.parent?.def?.defName
            + " pod=" + (transporter.parent?.thingIDNumber ?? -1)
            + " group=" + transporter.groupID;
    }

    internal sealed class PortalBulkSource : BulkLoadDestinationSource
    {
        private readonly MapPortal portal;

        public PortalBulkSource(MapPortal portal)
        {
            this.portal = portal;
        }

        public override Thing Destination => portal;
        public override List<TransferableOneWay> LeftToLoad => portal?.leftToLoad;
        public override string LogChannel => "portals";
        public override string JobLabel => "BulkHaulToPortal";

        public override Job MakeBulkJob(Pawn pawn) =>
            JobMaker.MakeJob(BulkLoadDefOf.BulkHaulToPortal, LocalTargetInfo.Invalid, portal);

        public override string DescribeTarget() =>
            " target=" + portal?.def?.defName;
    }
}
