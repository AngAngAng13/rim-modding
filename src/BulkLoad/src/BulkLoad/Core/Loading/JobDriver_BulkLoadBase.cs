using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public abstract class JobDriver_BulkLoadBase : JobDriver
    {
        protected Dictionary<ThingDef, int> _pickedUp = new Dictionary<ThingDef, int>();

        protected abstract Thing DestinationThing { get; }
        protected abstract bool HasMoreToLoad { get; }
        protected abstract ThingOwner GetDestinationContainer();
        protected abstract ThingCount FindFirstThingToLoad();
        protected abstract List<TransferableOneWay> GetLeftToLoad();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _pickedUp, "pickedUp", LookMode.Def, LookMode.Value);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (job.targetQueueA.NullOrEmpty())
            {
                if (!HasMoreToLoad)
                {
                    BulkLoadDiagnostics.Debug("jobs", "Reservation skipped: destination has nothing left to load.");
                    return false;
                }

                var first = FindFirstThingToLoad();
                if (first.Thing == null
                    || !BulkLoadServices.Planner.TryBuildTargetQueue(
                        pawn,
                        first,
                        GetLeftToLoad(),
                        out List<LocalTargetInfo> targets,
                        out List<int> counts,
                        destination: DestinationThing))
                {
                    BulkLoadDiagnostics.Debug("jobs", "Reservation failed: no source queue was available.");
                    return false;
                }

                job.targetQueueA = targets;
                job.countQueue = counts;
                ClaimQueueSources();
            }

            bool reserved = BulkLoadServices.Planner.TryReserveSourceQueue(pawn, job);
            if (reserved)
                BulkLoadServices.Coordinator.NotifyJobStarted(pawn, job, DestinationThing);
            BulkLoadDiagnostics.Debug(
                "jobs",
                "Reservation " + (reserved ? "succeeded" : "failed")
                + " job=" + job.def?.defName
                + " sources=" + job.targetQueueA.Count);
            BulkLoadDiagnostics.RecordModAction(
                pawn,
                "reservations",
                "job=" + job.def?.defName + " sources=" + job.targetQueueA.Count + " reserved=" + reserved);
            return reserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(jobCondition =>
            {
                BulkLoadServices.Coordinator.NotifyJobFinished(pawn, job);
                BulkLoadDiagnostics.Debug(
                    "jobs",
                    "Finished job=" + job.def?.defName
                    + " condition=" + jobCondition
                    + " trackedDefs=" + _pickedUp.Count
                    + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]");
                BulkLoadDiagnostics.RecordModAction(
                    pawn,
                    "job_finished",
                    "job=" + job.def?.defName + " condition=" + jobCondition);
                if (jobCondition == JobCondition.Succeeded)
                {
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "load_state",
                        "job=" + job.def?.defName + " state=completed");
                    return;
                }

                BulkLoadDiagnostics.RecordModAction(
                    pawn,
                    "load_state",
                    "job=" + job.def?.defName + " state=failed_returning_picked_up_items");
                BulkLoadServices.Pickups.DropTrackedItems(_pickedUp, pawn);
            });

            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => DestinationThing == null || (!HasMoreToLoad && _pickedUp.Count == 0));
            this.FailOn(() => TransporterUtility.WasLoadingCanceled(DestinationThing));
            this.FailOn(() => EnterPortalUtility.WasLoadingCanceled(DestinationThing));

            var extractNext = Toils_JobTransforms.ExtractNextTargetFromQueue(
                TargetIndex.A,
                failIfCountFromQueueTooBig: false);

            var scrubQueue = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A);

            var gotoThing = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            var takeThing = new Toil
            {
                initAction = () =>
                {
                    var thing = job.targetA.Thing;
                    if (thing == null || thing.Destroyed || !thing.Spawned || thing.IsForbidden(pawn))
                    {
                        BulkLoadServices.Planner.ReleaseSourceReservation(pawn, thing, job);
                        return;
                    }

                    int liveNeed = GetRemainingCountForDef(GetLeftToLoad(), thing.def);
                    if (liveNeed <= 0)
                    {
                        BulkLoadServices.Planner.ReleaseSourceReservation(pawn, thing, job);
                        BulkLoadDiagnostics.Debug(
                            "pickup",
                            "Skipped source=" + thing.def?.defName + " because destination no longer needs it.");
                        return;
                    }

                    int wantCount = Mathf.Min(job.count, thing.stackCount, liveNeed);
                    int canCarry = MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing);
                    int take = Mathf.Min(wantCount, canCarry);

                    if (take <= 0)
                    {
                        BulkLoadDiagnostics.Debug(
                            "pickup",
                            "Skipped source=" + thing.def?.defName + " want=" + wantCount + " carry=" + canCarry);
                        return;
                    }

                    var split = thing.SplitOff(take);
                    if (!pawn.inventory.GetDirectlyHeldThings().TryAdd(split, true))
                    {
                        GenPlace.TryPlaceThing(split, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                        BulkLoadDiagnostics.Warning(
                            "pickup",
                            "Could not move picked-up " + split.def?.defName + " into inventory; job will stop.");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    BulkLoadServices.Pickups.Track(_pickedUp, split.def, take);
                    BulkLoadServices.Planner.ReleaseSourceReservation(pawn, thing, job);
                    BulkLoadDiagnostics.Debug(
                        "pickup",
                        "Picked up " + take + "x " + split.def?.defName + " by " + pawn.LabelShort + " for job=" + job.def?.defName);
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "pickup",
                        "def=" + split.def?.defName + " count=" + take + " state=picked_up");
                }
            };
            var checkFull = new Toil
            {
                initAction = () =>
                {
                    if (MassUtility.EncumbrancePercent(pawn) >= BulkLoadAssignmentPolicy.ReservationEncumbrance)
                    {
                        BulkLoadDiagnostics.Debug(
                            "pickup",
                            "Stopping pickup at encumbrance=" + MassUtility.EncumbrancePercent(pawn));
                        job.targetQueueA?.Clear();
                        job.countQueue?.Clear();
                    }
                }
            };

            var gotoDestination = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            var beginDelivery = new Toil
            {
                initAction = () =>
                {
                    if (_pickedUp.Count == 0)
                        EndJobWith(JobCondition.Succeeded);
                }
            };

            yield return scrubQueue;
            yield return Toils_Jump.JumpIf(extractNext, () => !job.targetQueueA.NullOrEmpty());
            yield return beginDelivery;
            yield return extractNext;
            yield return gotoThing;
            yield return takeThing;
            yield return checkFull;
            yield return Toils_Jump.JumpIf(scrubQueue, () => !job.targetQueueA.NullOrEmpty());
            yield return beginDelivery;
            yield return gotoDestination;

            var unload = new Toil
            {
                initAction = () =>
                {
                    var dest = GetDestinationContainer();
                    if (dest == null)
                    {
                        BulkLoadDiagnostics.Error(
                            "deposit",
                            "Destination container was null for job=" + job.def?.defName);
                        return;
                    }

                    var inv = pawn.inventory.innerContainer;
                    var leftToLoad = GetLeftToLoad();

                    System.Text.StringBuilder needReport = null;
                    foreach (var tracked in _pickedUp)
                    {
                        int need = GetRemainingCountForDef(leftToLoad, tracked.Key);
                        if (needReport == null)
                            needReport = new System.Text.StringBuilder();
                        else
                            needReport.Append(",");
                        needReport.Append(tracked.Key?.defName).Append(":").Append(need).Append("/").Append(tracked.Value);
                    }
                    BulkLoadDiagnostics.Debug(
                        "deposit",
                        "Arrival need [" + (needReport?.ToString() ?? "-") + "] pawn=" + pawn.LabelShort
                        + " pod=" + (DestinationThing?.thingIDNumber ?? -1)
                        + " job=" + job.def?.defName);
                    BulkLoadDiagnostics.Debug(
                        "demand",
                        "Demand lines pod=" + (DestinationThing?.thingIDNumber ?? -1)
                        + " count=" + (leftToLoad?.Count ?? -1)
                        + " : " + DescribeDemandLines(leftToLoad));

                    for (int i = 0; i < inv.Count; i++)
                        PrepareDemandLineForTransfer(leftToLoad, inv[i]);

                    if (_pickedUp.Count == 0 && leftToLoad != null)
                    {
                        int transferredFallbackCount = 0;
                        for (int i = inv.Count - 1; i >= 0; i--)
                        {
                            var item = inv[i];
                            int neededCount = GetRemainingCountForDef(leftToLoad, item.def);
                            int toTransfer = Mathf.Min(neededCount, item.stackCount);
                            if (toTransfer <= 0)
                                continue;

                            if (TryTransferItem(item, toTransfer, dest, leftToLoad))
                            {
                                transferredFallbackCount += toTransfer;
                                BulkLoadServices.Coordinator.SettleTransaction(pawn, DestinationThing, item.def, toTransfer);
                            }
                        }
                        BulkLoadDiagnostics.Debug(
                            "deposit",
                            "Deposited fallback inventory count=" + transferredFallbackCount
                            + " for job=" + job.def?.defName);
                    }

                    int transferredCount = 0;
                    for (int i = inv.Count - 1; i >= 0; i--)
                    {
                        var item = inv[i];
                        if (!_pickedUp.ContainsKey(item.def))
                            continue;

                        int neededCount = GetRemainingCountForDef(leftToLoad, item.def);
                        int toTransfer = Mathf.Min(_pickedUp[item.def], item.stackCount, neededCount);
                        if (toTransfer <= 0)
                        {
                            BulkLoadDiagnostics.Debug(
                                "deposit",
                                "Skipped transfer def=" + item.def?.defName
                                + " stack=" + item.stackCount
                                + " tracked=" + _pickedUp[item.def]
                                + " needed=" + neededCount
                                + " pod=" + (DestinationThing?.thingIDNumber ?? -1));
                            continue;
                        }

                        bool transferred = TryTransferItem(item, toTransfer, dest, leftToLoad);

                        if (transferred)
                        {
                            _pickedUp[item.def] -= toTransfer;
                            if (_pickedUp[item.def] <= 0)
                                _pickedUp.Remove(item.def);
                            transferredCount += toTransfer;
                            BulkLoadDiagnostics.Debug(
                                "deposit",
                                "Transferred def=" + item.def?.defName
                                + " count=" + toTransfer
                                + " neededAfter=" + GetRemainingCountForDef(leftToLoad, item.def)
                                + " pod=" + (DestinationThing?.thingIDNumber ?? -1));
                            BulkLoadDiagnostics.Debug(
                                "demand",
                                "Demand lines pod=" + (DestinationThing?.thingIDNumber ?? -1)
                                + " count=" + (leftToLoad?.Count ?? -1)
                                + " : " + DescribeDemandLines(leftToLoad));
                            BulkLoadServices.Coordinator.SettleTransaction(pawn, DestinationThing, item.def, toTransfer);
                            BulkLoadDiagnostics.RecordModAction(
                                pawn,
                                "delivery",
                                "job=" + job.def?.defName
                                + " def=" + item.def?.defName
                                + " count=" + toTransfer
                                + " state=delivered");
                        }
                        else
                        {
                            BulkLoadDiagnostics.RecordModAction(
                                pawn,
                                "delivery_failed",
                                "job=" + job.def?.defName
                                + " def=" + item.def?.defName
                                + " count=" + toTransfer
                                + " state=returned_or_dropped");
                        }
                    }

                    BulkLoadDiagnostics.Debug(
                        "deposit",
                        "Deposited tracked count=" + transferredCount
                        + " pod=" + (DestinationThing?.thingIDNumber ?? -1)
                        + " for job=" + job.def?.defName);
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "deposit",
                        "job=" + job.def?.defName + " count=" + transferredCount);

                    if (_pickedUp.Count > 0)
                        BulkLoadServices.Pickups.DropTrackedItems(_pickedUp, pawn);
                }
            };
            yield return unload;
        }

        private void ClaimQueueSources()
        {
            if (job.targetQueueA.NullOrEmpty() || job.countQueue.NullOrEmpty())
                return;

            var plan = new Dictionary<ThingDef, int>();
            int entries = Mathf.Min(job.targetQueueA.Count, job.countQueue.Count);
            for (int i = 0; i < entries; i++)
            {
                Thing thing = job.targetQueueA[i].Thing;
                int count = job.countQueue[i];
                if (thing == null || count <= 0)
                    continue;
                plan[thing.def] = plan.TryGetValue(thing.def) + count;
            }

            BulkLoadServices.Coordinator.AddClaim(pawn, DestinationThing, plan);
        }

        private bool TryTransferItem(Thing item, int toTransfer, ThingOwner dest, List<TransferableOneWay> leftToLoad)
        {
            var inv = pawn.inventory.innerContainer;
            if (toTransfer == item.stackCount)
            {
                PrepareDemandLineForTransfer(leftToLoad, item);
                return inv.TryTransferToContainer(item, dest);
            }

            var split = item.SplitOff(toTransfer);
            PrepareDemandLineForTransfer(leftToLoad, split);
            return dest.TryAdd(split);
        }

        protected static int GetRemainingCountForDef(List<TransferableOneWay> leftToLoad, ThingDef def)
        {
            if (leftToLoad == null || def == null)
                return 0;

            int remaining = 0;
            foreach (var transferable in leftToLoad)
            {
                if (transferable.CountToTransfer <= 0 || transferable.ThingDef != def)
                    continue;
                remaining += transferable.CountToTransfer;
            }
            return remaining;
        }

        protected static void PrepareDemandLineForTransfer(List<TransferableOneWay> leftToLoad, Thing item)
        {
            if (leftToLoad == null || item == null)
                return;
            var line = TransferableUtility.TransferableMatchingDesperate(item, leftToLoad, TransferAsOneMode.PodsOrCaravanPacking);
            if (line != null && !line.things.Contains(item))
                line.things.Add(item);
        }

        protected static string DescribeDemandLines(List<TransferableOneWay> leftToLoad)
        {
            if (leftToLoad == null)
                return "-";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < leftToLoad.Count; i++)
            {
                var t = leftToLoad[i];
                if (i > 0)
                    sb.Append("|");
                sb.Append(i).Append(":").Append(t?.ThingDef?.defName ?? "null")
                    .Append("=").Append(t?.CountToTransfer ?? -999)
                    .Append("#").Append(t?.GetHashCode() ?? 0);
            }
            return sb.ToString();
        }

        public override string GetReport()
        {
            if (DestinationThing != null)
                return "Bulk loading " + DestinationThing.LabelShort + ".";
            return "Bulk loading.";
        }
    }
}
