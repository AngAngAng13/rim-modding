using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    internal interface ILoadingPlanner
    {
        ThingCount FindThingToLoad(
            Pawn pawn,
            List<TransferableOneWay> leftToLoad,
            Dictionary<ThingDef, int> demandLimit = null,
            bool logSelection = true,
            Thing loadDestination = null);

        bool TryBuildTargetQueue(
            Pawn pawn,
            ThingCount first,
            List<TransferableOneWay> leftToLoad,
            out List<LocalTargetInfo> targets,
            out List<int> counts,
            Dictionary<ThingDef, int> demandLimit = null,
            Thing destination = null);

        bool TryReserveSourceQueue(Pawn pawn, Job job);
        void ReleaseSourceReservation(Pawn pawn, Thing thing, Job job);
    }

    internal sealed class LoadingPlanner : ILoadingPlanner
    {
        private readonly IClaimLedger ledger;

        public LoadingPlanner(IClaimLedger ledger)
        {
            this.ledger = ledger;
        }

        public ThingCount FindThingToLoad(
            Pawn pawn,
            List<TransferableOneWay> leftToLoad,
            Dictionary<ThingDef, int> demandLimit = null,
            bool logSelection = true,
            Thing loadDestination = null)
        {
            if (pawn == null || leftToLoad.NullOrEmpty())
                return default;

            Thing bestThing = null;
            int bestCount = 0;
            float bestDistance = float.MaxValue;

            foreach (TransferableOneWay transferable in leftToLoad)
            {
                if (transferable == null || transferable.CountToTransfer <= 0 || transferable.things.NullOrEmpty())
                    continue;

                for (int i = 0; i < transferable.things.Count; i++)
                {
                    Thing thing = transferable.things[i];
                    if (!IsValidSource(pawn, thing))
                        continue;

                    int requested = transferable.CountToTransfer;
                    if (demandLimit != null)
                        requested = Mathf.Min(requested, demandLimit.TryGetValue(thing.def));
                    if (loadDestination != null)
                    {
                        requested -= ledger.ClaimedByOthers(loadDestination, pawn, thing.def);
                        if (requested <= 0)
                            continue;
                    }

                    int count = GetAvailableCount(pawn, thing, requested);
                    if (count <= 0)
                        continue;

                    float distance = pawn.Position.DistanceToSquared(thing.Position);
                    if (distance > bestDistance)
                        continue;

                    if (!pawn.CanReach(thing, PathEndMode.ClosestTouch, pawn.NormalMaxDanger()))
                        continue;

                    if (distance < bestDistance || bestThing == null || thing.thingIDNumber < bestThing.thingIDNumber)
                    {
                        bestThing = thing;
                        bestCount = count;
                        bestDistance = distance;
                    }
                }
            }

            if (bestThing == null)
                return default;

            if (logSelection)
            {
                BulkLoadDiagnostics.Debug(
                    "planning",
                    "Selected nearest source=" + bestThing.def?.defName
                    + " count=" + bestCount
                    + " distanceSq=" + bestDistance.ToString("F0"));
            }
            return new ThingCount(bestThing, bestCount);
        }

        public bool TryBuildTargetQueue(
            Pawn pawn,
            ThingCount first,
            List<TransferableOneWay> leftToLoad,
            out List<LocalTargetInfo> targets,
            out List<int> counts,
            Dictionary<ThingDef, int> demandLimit = null,
            Thing destination = null)
        {
            targets = new List<LocalTargetInfo>();
            counts = new List<int>();

            if (pawn == null || first.Thing == null || first.Thing is Pawn || leftToLoad.NullOrEmpty())
                return false;

            var remaining = new Dictionary<TransferableOneWay, int>();
            var remainingByDef = new Dictionary<ThingDef, int>();
            float remainingMass = MassUtility.FreeSpace(pawn) * BulkLoadAssignmentPolicy.ReservationEncumbrance;
            if (remainingMass <= 0f)
                return false;

            foreach (TransferableOneWay transferable in leftToLoad)
            {
                if (transferable == null || transferable.CountToTransfer <= 0 || transferable.ThingDef == null)
                    continue;

                int demand = transferable.CountToTransfer;
                if (demandLimit != null)
                    demand = Mathf.Min(demand, demandLimit.TryGetValue(transferable.ThingDef));
                if (demand <= 0)
                    continue;

                remaining[transferable] = demand;
                remainingByDef[transferable.ThingDef] = remainingByDef.TryGetValue(transferable.ThingDef) + demand;
            }

            if (destination != null)
            {
                foreach (ThingDef def in remainingByDef.Keys.ToList())
                {
                    int open = remainingByDef[def] - ledger.ClaimedByOthers(destination, pawn, def);
                    remainingByDef[def] = Mathf.Max(0, open);
                }
            }

            if (!TryGetDemandForThing(first.Thing, leftToLoad, remaining, out TransferableOneWay firstDemand))
                return false;

            int firstCount = GetAvailableCount(
                pawn,
                first.Thing,
                Mathf.Min(
                    first.Count,
                    remaining[firstDemand],
                    remainingByDef.TryGetValue(first.Thing.def)));
            firstCount = LimitCountByMass(first.Thing, firstCount, remainingMass);
            if (firstCount <= 0)
                return false;

            AddTarget(targets, counts, first.Thing, firstCount);
            remaining[firstDemand] -= firstCount;
            remainingByDef[first.Thing.def] -= firstCount;
            remainingMass -= firstCount * first.Thing.GetStatValue(StatDefOf.Mass);

            var candidates = new HashSet<Thing>();
            foreach (TransferableOneWay transferable in leftToLoad)
            {
                if (transferable == null || transferable.CountToTransfer <= 0 || transferable.things.NullOrEmpty())
                    continue;

                for (int i = 0; i < transferable.things.Count; i++)
                    candidates.Add(transferable.things[i]);
            }

            IntVec3 clusterCenter = first.Thing.Position;
            float maxDistanceSquared = BulkLoadAssignmentPolicy.ClusterRadius * BulkLoadAssignmentPolicy.ClusterRadius;
            TraverseParms traverseParms = TraverseParms.For(pawn);
            foreach (Thing thing in candidates
                .Where(t => t != null && t != first.Thing)
                .OrderBy(t => t.Position.DistanceToSquared(clusterCenter))
                .ThenBy(t => t.thingIDNumber))
            {
                if (remainingMass <= 0f)
                    break;

                if (!IsValidSource(pawn, thing))
                    continue;
                if (thing.Position.DistanceToSquared(clusterCenter) > maxDistanceSquared)
                    continue;
                if (!pawn.Map.reachability.CanReach(clusterCenter, thing, PathEndMode.ClosestTouch, traverseParms))
                    continue;
                if (!TryGetDemandForThing(thing, leftToLoad, remaining, out TransferableOneWay demand))
                    continue;

                int count = GetAvailableCount(
                    pawn,
                    thing,
                    Mathf.Min(remaining[demand], remainingByDef.TryGetValue(thing.def)));
                count = LimitCountByMass(thing, count, remainingMass);
                if (count <= 0)
                    continue;

                AddTarget(targets, counts, thing, count);
                remaining[demand] -= count;
                remainingByDef[thing.def] -= count;
                remainingMass -= count * thing.GetStatValue(StatDefOf.Mass);
            }

            BulkLoadDiagnostics.Debug(
                "planning",
                "Built nearest-cluster queue sources=" + targets.Count
                + " first=" + first.Thing.def?.defName
                + " clusterRadius=" + BulkLoadAssignmentPolicy.ClusterRadius.ToString("F0")
                + " reservedMass=" + (MassUtility.FreeSpace(pawn) * BulkLoadAssignmentPolicy.ReservationEncumbrance - remainingMass).ToString("F1"));
            return targets.Count > 0;
        }

        public bool TryReserveSourceQueue(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || job.targetQueueA.NullOrEmpty() || job.countQueue.NullOrEmpty())
                return false;

            int count = Mathf.Min(job.targetQueueA.Count, job.countQueue.Count);
            for (int i = 0; i < count; i++)
            {
                Thing thing = job.targetQueueA[i].Thing;
                int stackCount = job.countQueue[i];
                bool alreadyReserved = thing != null
                    && pawn.Map.reservationManager.ReservedBy(thing, pawn, job);
                if (thing == null
                    || stackCount <= 0
                    || (!alreadyReserved && !pawn.Reserve(thing, job, 1, stackCount, null, errorOnFailed: false)))
                {
                    BulkLoadDiagnostics.Warning(
                        "reservations",
                        "Failed to reserve source=" + thing?.def?.defName
                        + " count=" + stackCount
                        + " job=" + job.def?.defName);
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "reservation_failed",
                        "job=" + job.def?.defName
                        + " source=" + thing?.def?.defName
                        + " count=" + stackCount);
                    pawn.ClearReservationsForJob(job);
                    return false;
                }

                if (!alreadyReserved)
                {
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "reservation_acquired",
                        "job=" + job.def?.defName
                        + " source=" + thing.def?.defName
                        + " count=" + stackCount
                        + " state=reserved");
                }
            }

            return true;
        }

        public void ReleaseSourceReservation(Pawn pawn, Thing thing, Job job)
        {
            if (pawn?.Map == null || thing == null || job == null)
                return;

            LocalTargetInfo target = thing;
            if (pawn.Map.reservationManager.ReservedBy(target, pawn, job))
            {
                pawn.Map.reservationManager.Release(target, pawn, job);
                BulkLoadDiagnostics.RecordModAction(
                    pawn,
                    "reservation_released",
                    "job=" + job.def?.defName
                    + " source=" + thing.def?.defName
                    + " state=picked_up");
            }
        }

        private static bool TryGetDemandForThing(
            Thing thing,
            List<TransferableOneWay> leftToLoad,
            Dictionary<TransferableOneWay, int> remaining,
            out TransferableOneWay demand)
        {
            for (int i = 0; i < leftToLoad.Count; i++)
            {
                TransferableOneWay transferable = leftToLoad[i];
                if (transferable != null
                    && remaining.TryGetValue(transferable, out int count)
                    && count > 0
                    && transferable.things != null
                    && transferable.things.Contains(thing))
                {
                    demand = transferable;
                    return true;
                }
            }

            demand = null;
            return false;
        }

        private static int GetAvailableCount(Pawn pawn, Thing thing, int requested)
        {
            if (requested <= 0 || !IsValidSource(pawn, thing))
                return 0;

            int reservedSpace = pawn.Map.reservationManager.CanReserveStack(
                pawn,
                thing,
                maxPawns: 1,
                layer: null,
                ignoreOtherReservations: false);
            int carrySpace = pawn.carryTracker.AvailableStackSpace(thing.def);
            int massSpace = CountToPickUpUntilOverEncumbered(pawn, thing);
            return Mathf.Min(requested, thing.stackCount, reservedSpace, carrySpace, massSpace);
        }

        private static int LimitCountByMass(Thing thing, int requested, float remainingMass)
        {
            if (requested <= 0 || remainingMass <= 0f)
                return 0;

            float mass = thing.GetStatValue(StatDefOf.Mass);
            if (mass <= 0f)
                return requested;

            return Mathf.Min(requested, Mathf.FloorToInt(remainingMass / mass));
        }

        private static int CountToPickUpUntilOverEncumbered(Pawn pawn, Thing thing)
        {
            float mass = thing.GetStatValue(StatDefOf.Mass);
            if (mass <= 0f)
                return thing.stackCount;

            return Mathf.FloorToInt(MassUtility.FreeSpace(pawn) / mass);
        }

        private static bool IsValidSource(Pawn pawn, Thing thing)
        {
            return thing != null
                && thing.Spawned
                && thing.def.EverHaulable
                && !(thing is Pawn)
                && !thing.IsForbidden(pawn);
        }

        private static void AddTarget(List<LocalTargetInfo> targets, List<int> counts, Thing thing, int count)
        {
            if (count <= 0)
                return;

            targets.Add(thing);
            counts.Add(count);
        }
    }
}
