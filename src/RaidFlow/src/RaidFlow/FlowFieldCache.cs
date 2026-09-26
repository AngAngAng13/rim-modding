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
            if (!BuildKey(request, out FlowFieldKey key, out TraverseParms parms, out AvoidGrid avoidGrid))
                return null;
            Map map = request.map;
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

        private static bool BuildKey(PathRequest request, out FlowFieldKey key, out TraverseParms parms, out AvoidGrid avoidGrid)
        {
            key = default;
            parms = default;
            avoidGrid = null;
            Pawn pawn = request?.pawn;
            Map map = request?.map;
            if (pawn == null || map == null || map.Disposed)
                return false;
            parms = request.TraverseParms;
            pawn.TryGetAvoidGrid(out avoidGrid);
            key = new FlowFieldKey(map, request.ExactDestination, parms.canBashDoors, parms.canBashFences, avoidGrid != null, request.Tuning);
            return true;
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
            List<IntVec3> nodes = field.TraceToGoal(map, request.pawn, request.Start, request.ExactDestination);
            if (nodes == null || !LiveRoute(map, request.TraverseParms, nodes))
            {
                if (BuildKey(request, out FlowFieldKey stale, out _, out _))
                    fields.Remove(stale);
                return false;
            }
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

        private static bool LiveRoute(Map map, TraverseParms parms, List<IntVec3> nodes)
        {
            PathGrid grid = map.pathing.For(parms).pathGrid;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!LiveEnterable(map, grid, parms, nodes[i]))
                    return false;
                if (i > 0)
                {
                    int dx = nodes[i].x - nodes[i - 1].x;
                    int dz = nodes[i].z - nodes[i - 1].z;
                    if (dx != 0 && dz != 0)
                    {
                        var sideA = new IntVec3(nodes[i - 1].x + dx, 0, nodes[i - 1].z);
                        var sideB = new IntVec3(nodes[i - 1].x, 0, nodes[i - 1].z + dz);
                        if (!sideA.InBounds(map) || !sideB.InBounds(map))
                            return false;
                        if (!LiveEnterable(map, grid, parms, sideA) || !LiveEnterable(map, grid, parms, sideB))
                            return false;
                    }
                }
            }
            return true;
        }

        private static bool LiveEnterable(Map map, PathGrid grid, TraverseParms parms, IntVec3 cell)
        {
            if (grid.Cost(cell) < PathGrid.ImpassableCost)
                return true;
            if (parms.canBashDoors && cell.GetDoor(map) != null)
                return true;
            return parms.canBashFences && cell.GetEdifice(map)?.def.IsFence == true;
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
