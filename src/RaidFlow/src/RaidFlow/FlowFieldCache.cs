using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RaidFlow
{
    public static class FlowFieldCache
    {
        private const int MaxFields = 8;
        private const int FieldLifetimeTicks = 300;

        private static readonly Dictionary<FlowFieldKey, FlowField> fields = new Dictionary<FlowFieldKey, FlowField>();

        public static FlowField GetOrCreate(PathRequest request)
        {
            Pawn pawn = request.pawn;
            Map map = request.map;
            TraverseParms parms = request.TraverseParms;
            pawn.TryGetAvoidGrid(out AvoidGrid avoidGrid);
            var key = new FlowFieldKey(map, request.ExactDestination, parms.canBashDoors, parms.canBashFences, avoidGrid != null, request.Tuning);
            int now = GenTicks.TicksGame;
            if (fields.TryGetValue(key, out FlowField field))
            {
                if (now - field.TickComputed < FieldLifetimeTicks)
                    return field;
                fields.Remove(key);
            }
            if (fields.Count >= MaxFields)
                EvictOldest();
            field = FlowField.Compute(map, key, parms, avoidGrid);
            if (field == null || !field.Valid)
                return null;
            fields[key] = field;
            return field;
        }

        public static bool TryResolve(PathRequest request)
        {
            if (request == null)
                return false;
            return false;
        }

        private static void EvictOldest()
        {
            FlowFieldKey oldest = default;
            int oldestTick = int.MaxValue;
            bool found = false;
            foreach (var pair in fields)
            {
                if (!found || pair.Value.TickComputed < oldestTick)
                {
                    oldest = pair.Key;
                    oldestTick = pair.Value.TickComputed;
                    found = true;
                }
            }
            if (found)
                fields.Remove(oldest);
        }
    }
}
