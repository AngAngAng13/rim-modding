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
            if (pawn == null || map == null || map.Disposed)
                return null;
            TraverseParms parms = request.TraverseParms;
            pawn.TryGetAvoidGrid(out AvoidGrid avoidGrid);
            var key = new FlowFieldKey(map, request.ExactDestination, parms.canBashDoors, parms.canBashFences, avoidGrid != null, request.Tuning);
            int now = GenTicks.TicksGame;
            if (fields.TryGetValue(key, out FlowField field))
            {
                if (!map.Disposed && now - field.TickComputed < FieldLifetimeTicks)
                    return field;
                fields.Remove(key);
            }
            SweepDisposed();
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
            Map map = request.map;
            if (map == null || request.pawn == null)
                return false;
            FlowField field = GetOrCreate(request);
            if (field == null)
                return false;
            List<IntVec3> nodes = field.TraceToGoal(map, request.Start);
            if (nodes == null)
                return false;
            PawnPath path = map.pawnPathPool.GetPath();
            for (int i = nodes.Count - 1; i >= 0; i--)
                path.AddNode(nodes[i]);
            int startIndex = map.cellIndices.CellToIndex(request.Start);
            if (!PawnPathInit.SetStarted(path, nodes.Count - 1, field.CostAt(startIndex)))
            {
                path.Dispose();
                return false;
            }
            request.Resolve(path);
            return true;
        }

        private static void SweepDisposed()
        {
            FlowFieldKey stale = default;
            bool found = false;
            foreach (var pair in fields)
            {
                if (!pair.Key.Map.Disposed)
                    continue;
                stale = pair.Key;
                found = true;
                break;
            }
            if (found)
            {
                fields.Remove(stale);
                SweepDisposed();
            }
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
