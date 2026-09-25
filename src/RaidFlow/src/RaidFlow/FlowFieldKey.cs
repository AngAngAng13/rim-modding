using System;
using Verse;

namespace RaidFlow
{
    public struct FlowFieldKey : IEquatable<FlowFieldKey>
    {
        public FlowFieldKey(Map map, IntVec3 goal, bool bashDoors, bool bashFences, bool avoidDanger, PathFinderCostTuning tuning)
        {
            Map = map;
            Goal = goal;
            BashDoors = bashDoors;
            BashFences = bashFences;
            AvoidDanger = avoidDanger;
            Tuning = tuning;
        }

        public Map Map { get; }
        public IntVec3 Goal { get; }
        public bool BashDoors { get; }
        public bool BashFences { get; }
        public bool AvoidDanger { get; }
        public PathFinderCostTuning Tuning { get; }

        public bool Equals(FlowFieldKey other)
        {
            return Map == other.Map
                && Goal == other.Goal
                && BashDoors == other.BashDoors
                && BashFences == other.BashFences
                && AvoidDanger == other.AvoidDanger
                && Tuning.Equals(other.Tuning);
        }

        public override bool Equals(object obj)
        {
            return obj is FlowFieldKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Map == null ? 0 : Map.GetHashCode();
                hash = hash * 397 ^ Goal.GetHashCode();
                hash = hash * 397 ^ BashDoors.GetHashCode();
                hash = hash * 397 ^ BashFences.GetHashCode();
                hash = hash * 397 ^ AvoidDanger.GetHashCode();
                return hash * 397 ^ Tuning.GetHashCode();
            }
        }
    }
}
