using System;
using System.Collections.Generic;
using Verse;

namespace BulkLoad
{
    internal interface ITurnTaker
    {
        bool ShouldOffer(Pawn pawn, Thing destination, Func<Pawn, bool> canLoad);
        bool TryClaimOffer(Pawn pawn, Thing destination);
        void CancelOffer(Pawn pawn, Thing destination);
        void NotifyAssigned(Pawn pawn, Thing destination);
        bool HasValidOffer(Thing destination);
        string DescribeOffer(Thing destination);
    }

    internal sealed class TurnTaker : ITurnTaker
    {
        private sealed class DestinationState
        {
            public int lastAssignedPawnId = -1;
            public Pawn offeredPawn;
            public int offeredTick;
        }

        private const int OfferLeaseTicks = 15;

        private readonly Dictionary<Thing, DestinationState> states = new Dictionary<Thing, DestinationState>();

        public bool ShouldOffer(Pawn pawn, Thing destination, Func<Pawn, bool> canLoad)
        {
            if (pawn == null || destination == null || canLoad == null)
                return false;

            DestinationState state = GetState(destination);
            PruneOffer(state, destination);
            if (state.offeredPawn != null)
            {
                if (!canLoad(state.offeredPawn))
                {
                    state.offeredPawn = null;
                }
                else
                {
                    return state.offeredPawn == pawn;
                }
            }

            Pawn nextPawn = FindNextPawn(destination, canLoad, state.lastAssignedPawnId);
            if (nextPawn != pawn)
                return false;

            state.offeredPawn = pawn;
            state.offeredTick = GenTicks.TicksGame;
            BulkLoadDiagnostics.Debug(
                "jobs",
                "Offered loading turn to " + pawn.LabelShort
                + " destination=" + destination.def?.defName);
            return true;
        }

        public bool TryClaimOffer(Pawn pawn, Thing destination)
        {
            if (pawn == null || destination == null || !states.TryGetValue(destination, out DestinationState state))
                return false;

            PruneOffer(state, destination);
            if (state.offeredPawn != pawn)
                return false;

            state.offeredPawn = null;
            state.lastAssignedPawnId = pawn.thingIDNumber;
            return true;
        }

        public void CancelOffer(Pawn pawn, Thing destination)
        {
            if (pawn == null || destination == null || !states.TryGetValue(destination, out DestinationState state))
                return;

            if (state.offeredPawn == pawn)
                state.offeredPawn = null;
        }

        public void NotifyAssigned(Pawn pawn, Thing destination)
        {
            if (pawn == null || destination == null || !states.TryGetValue(destination, out DestinationState state))
                return;

            state.offeredPawn = null;
            state.lastAssignedPawnId = pawn.thingIDNumber;
        }

        public bool HasValidOffer(Thing destination)
        {
            if (destination == null || !states.TryGetValue(destination, out DestinationState state))
                return false;

            PruneOffer(state, destination);
            return state.offeredPawn != null
                && GenTicks.TicksGame - state.offeredTick <= OfferLeaseTicks
                && BulkLoadAssignmentPolicy.IsEligibleHauler(state.offeredPawn, destination);
        }

        public string DescribeOffer(Thing destination)
        {
            if (destination == null || !states.TryGetValue(destination, out DestinationState state))
                return "-";
            if (state.offeredPawn == null)
                return "none(last=" + state.lastAssignedPawnId + ")";
            return state.offeredPawn.LabelShort + "(age=" + (GenTicks.TicksGame - state.offeredTick) + "t)";
        }

        private DestinationState GetState(Thing destination)
        {
            if (!states.TryGetValue(destination, out DestinationState state))
            {
                state = new DestinationState();
                states[destination] = state;
            }
            return state;
        }

        private static void PruneOffer(DestinationState state, Thing destination)
        {
            if (state.offeredPawn == null)
                return;

            if (GenTicks.TicksGame - state.offeredTick <= OfferLeaseTicks
                && BulkLoadAssignmentPolicy.IsEligibleHauler(state.offeredPawn, destination))
                return;

            state.lastAssignedPawnId = state.offeredPawn.thingIDNumber;
            state.offeredPawn = null;
        }

        private static Pawn FindNextPawn(Thing destination, Func<Pawn, bool> canLoad, int lastAssignedPawnId)
        {
            if (destination.Map?.mapPawns == null)
                return null;

            Pawn firstPawn = null;
            Pawn nextPawn = null;
            IReadOnlyList<Pawn> pawns = destination.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || !candidate.RaceProps.Humanlike)
                    continue;
                if (!BulkLoadAssignmentPolicy.IsEligibleHauler(candidate, destination) || !canLoad(candidate))
                    continue;

                if (firstPawn == null || candidate.thingIDNumber < firstPawn.thingIDNumber)
                    firstPawn = candidate;

                if (lastAssignedPawnId >= 0
                    && candidate.thingIDNumber > lastAssignedPawnId
                    && (nextPawn == null || candidate.thingIDNumber < nextPawn.thingIDNumber))
                {
                    nextPawn = candidate;
                }
            }

            return nextPawn ?? firstPawn;
        }
    }
}
