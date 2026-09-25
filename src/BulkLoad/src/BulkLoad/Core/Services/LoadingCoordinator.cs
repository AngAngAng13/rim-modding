using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    internal interface ILoadingCoordinator : IClaimLedger
    {
        bool ShouldOffer(Pawn pawn, Thing destination, Func<Pawn, bool> canLoad);
        bool TryClaimOffer(Pawn pawn, Thing destination);
        void CancelOffer(Pawn pawn, Thing destination);
        void NotifyJobStarted(Pawn pawn, Job job, Thing destination);
        void NotifyJobFinished(Pawn pawn, Job job);
        bool HasActiveJobFor(Thing destination);
        bool IsActiveLoader(Pawn pawn, Thing destination);
        string DescribeBlockingState(Thing destination);
    }

    internal sealed class LoadingCoordinator : ILoadingCoordinator
    {
        private readonly IClaimLedger ledger;
        private readonly IActiveJobRegistry registry;
        private readonly ITurnTaker turns;

        public LoadingCoordinator(IClaimLedger ledger, IActiveJobRegistry registry, ITurnTaker turns)
        {
            this.ledger = ledger;
            this.registry = registry;
            this.turns = turns;
        }

        public bool ShouldOffer(Pawn pawn, Thing destination, Func<Pawn, bool> canLoad) =>
            turns.ShouldOffer(pawn, destination, canLoad);

        public bool TryClaimOffer(Pawn pawn, Thing destination) =>
            turns.TryClaimOffer(pawn, destination);

        public void CancelOffer(Pawn pawn, Thing destination) =>
            turns.CancelOffer(pawn, destination);

        public void NotifyJobStarted(Pawn pawn, Job job, Thing destination)
        {
            if (pawn == null || job == null)
                return;

            registry.Register(pawn, job, destination);
            turns.NotifyAssigned(pawn, destination);
            BulkLoadDiagnostics.Debug(
                "jobs",
                "Started tracked job=" + job.def?.defName
                + " pawn=" + pawn.LabelShort
                + " destination=" + destination?.def?.defName
                + "_" + (destination?.thingIDNumber ?? -1));
            BulkLoadDiagnostics.RecordModAction(
                pawn,
                "job_started",
                "job=" + job.def?.defName
                + " destination=" + destination?.def?.defName);
        }

        public void NotifyJobFinished(Pawn pawn, Job job)
        {
            if (pawn == null || job == null)
                return;

            bool wasTracked = registry.Unregister(pawn, job);
            pawn.ClearReservationsForJob(job);
            ledger.ReleaseClaims(pawn);
            if (!wasTracked)
                return;

            BulkLoadDiagnostics.Debug(
                "jobs",
                "Finished tracked job=" + job.def?.defName
                + " pawn=" + pawn.LabelShort);
        }

        public bool HasActiveJobFor(Thing destination)
        {
            if (destination == null)
                return false;
            return registry.HasActiveJobFor(destination) || turns.HasValidOffer(destination);
        }

        public bool IsActiveLoader(Pawn pawn, Thing destination) =>
            registry.IsActiveLoader(pawn, destination);

        public string DescribeBlockingState(Thing destination) =>
            "active=[" + registry.DescribeActive() + "]"
            + " offer=[" + turns.DescribeOffer(destination) + "]"
            + " claims=[" + ledger.DescribeClaims(destination) + "]";

        public int ClaimedByOthers(Thing destination, Pawn pawn, ThingDef def) =>
            ledger.ClaimedByOthers(destination, pawn, def);

        public void AddClaim(Pawn pawn, Thing destination, Dictionary<ThingDef, int> plan) =>
            ledger.AddClaim(pawn, destination, plan);

        public void SettleTransaction(Pawn pawn, Thing destination, ThingDef def, int count) =>
            ledger.SettleTransaction(pawn, destination, def, count);

        public void ReleaseClaims(Pawn pawn) =>
            ledger.ReleaseClaims(pawn);

        public string DescribeClaims(Thing destination) =>
            ledger.DescribeClaims(destination);
    }
}
