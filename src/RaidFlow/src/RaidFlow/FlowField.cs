using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RaidFlow
{
    public sealed class FlowField
    {
        private const int Unreached = int.MaxValue;

        private readonly int[] costs;
        private readonly int width;

        private FlowField(int cells, int width, int tick)
        {
            costs = new int[cells];
            this.width = width;
            TickComputed = tick;
            for (int i = 0; i < costs.Length; i++)
                costs[i] = Unreached;
        }

        public int TickComputed { get; }
        public bool Valid { get; private set; }
        public int NumCells => costs.Length;

        public int CostAt(int index)
        {
            return costs[index];
        }

        public List<IntVec3> TraceToGoal(Map map, IntVec3 start)
        {
            if (!start.InBounds(map))
                return null;
            int current = map.cellIndices.CellToIndex(start);
            if (costs[current] == Unreached)
                return null;
            var nodes = new List<IntVec3>(64);
            nodes.Add(start);
            int guard = costs.Length;
            while (guard-- > 0)
            {
                int currentCost = costs[current];
                if (currentCost == 0)
                    return nodes.Count >= 2 ? nodes : null;
                int cx = current % width;
                int cz = current / width;
                int best = -1;
                int bestCost = currentCost;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;
                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= map.Size.x || nz >= map.Size.z)
                            continue;
                        int next = nz * width + nx;
                        int nextCost = costs[next];
                        if (nextCost >= bestCost)
                            continue;
                        if (dx != 0 && dz != 0 && (costs[cz * width + nx] == Unreached || costs[nz * width + cx] == Unreached))
                            continue;
                        best = next;
                        bestCost = nextCost;
                    }
                }
                if (best < 0)
                    return null;
                current = best;
                nodes.Add(new IntVec3(current % width, 0, current / width));
            }
            return null;
        }

        public static FlowField Compute(Map map, FlowFieldKey key, TraverseParms parms, AvoidGrid avoidGrid)
        {
            int cells = map.cellIndices.NumGridCells;
            var field = new FlowField(cells, map.Size.x, GenTicks.TicksGame);
            PathGrid grid = map.pathing.For(parms).pathGrid;
            var heap = new CellHeap(field.costs);
            if (!SeedGoal(map, key, parms, grid, avoidGrid, field, heap))
                return field;
            RunDijkstra(map, key, parms, grid, avoidGrid, field, heap);
            field.Valid = true;
            return field;
        }

        private static bool SeedGoal(Map map, FlowFieldKey key, TraverseParms parms, PathGrid grid, AvoidGrid avoidGrid, FlowField field, CellHeap heap)
        {
            int goalIndex = map.cellIndices.CellToIndex(key.Goal);
            if (EnterCost(map, key, parms, grid, avoidGrid, field, goalIndex) >= 0)
            {
                field.costs[goalIndex] = 0;
                heap.Push(goalIndex);
                return true;
            }
            bool seeded = false;
            IntVec3 goal = key.Goal;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;
                    IntVec3 neighbor = goal + new IntVec3(dx, 0, dz);
                    if (!neighbor.InBounds(map))
                        continue;
                    int index = map.cellIndices.CellToIndex(neighbor);
                    int enter = EnterCost(map, key, parms, grid, avoidGrid, field, index);
                    if (enter < 0)
                        continue;
                    field.costs[index] = enter;
                    heap.Push(index);
                    seeded = true;
                }
            }
            return seeded;
        }

        private static void RunDijkstra(Map map, FlowFieldKey key, TraverseParms parms, PathGrid grid, AvoidGrid avoidGrid, FlowField field, CellHeap heap)
        {
            int w = field.width;
            int mapW = map.Size.x;
            int mapH = map.Size.z;
            while (heap.Count > 0)
            {
                int current = heap.Pop();
                int currentCost = field.costs[current];
                int cx = current % w;
                int cz = current / w;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;
                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= mapW || nz >= mapH)
                            continue;
                        int next = nz * w + nx;
                        int enter = EnterCost(map, key, parms, grid, avoidGrid, field, next);
                        if (enter < 0)
                            continue;
                        if (dx != 0 && dz != 0 && !DiagonalOpen(map, key, parms, grid, avoidGrid, field, cx, cz, nx, nz))
                            continue;
                        int step = dx != 0 && dz != 0 ? enter * 141 / 100 : enter;
                        if (step < 1)
                            step = 1;
                        int total = currentCost + step;
                        if (total < field.costs[next])
                        {
                            field.costs[next] = total;
                            heap.Push(next);
                        }
                    }
                }
            }
        }

        private static bool DiagonalOpen(Map map, FlowFieldKey key, TraverseParms parms, PathGrid grid, AvoidGrid avoidGrid, FlowField field, int cx, int cz, int nx, int nz)
        {
            int sideA = cz * field.width + nx;
            int sideB = nz * field.width + cx;
            return EnterCost(map, key, parms, grid, avoidGrid, field, sideA) >= 0
                && EnterCost(map, key, parms, grid, avoidGrid, field, sideB) >= 0;
        }

        private static int EnterCost(Map map, FlowFieldKey key, TraverseParms parms, PathGrid grid, AvoidGrid avoidGrid, FlowField field, int index)
        {
            int x = index % field.width;
            int z = index / field.width;
            var cell = new IntVec3(x, 0, z);
            int perceived = grid.Cost(cell);
            if (perceived < PathGrid.ImpassableCost)
                return AddAvoid(avoidGrid, index, perceived, key);
            Building_Door door = cell.GetDoor(map);
            if (door != null && key.BashDoors)
            {
                int bash = key.Tuning.costBlockedDoor + (int)(key.Tuning.costBlockedDoorPerHitPoint * door.HitPoints);
                return AddAvoid(avoidGrid, index, bash, key);
            }
            if (key.BashFences && cell.GetEdifice(map)?.def.IsFence == true)
                return AddAvoid(avoidGrid, index, key.Tuning.costBlockedDoor, key);
            return -1;
        }

        private static int AddAvoid(AvoidGrid avoidGrid, int index, int cost, FlowFieldKey key)
        {
            if (avoidGrid != null && avoidGrid.Grid[index] > 0)
                return cost + key.Tuning.costDanger;
            return cost;
        }

        private sealed class CellHeap
        {
            private readonly int[] costs;
            private readonly int[] heap;
            private int count;

            public CellHeap(int[] costs)
            {
                this.costs = costs;
                heap = new int[costs.Length];
            }

            public int Count => count;

            public void Push(int index)
            {
                int i = count;
                count++;
                heap[i] = index;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (costs[heap[parent]] <= costs[heap[i]])
                        break;
                    int swap = heap[parent];
                    heap[parent] = heap[i];
                    heap[i] = swap;
                    i = parent;
                }
            }

            public int Pop()
            {
                int top = heap[0];
                count--;
                heap[0] = heap[count];
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = left + 1;
                    int smallest = i;
                    if (left < count && costs[heap[left]] < costs[heap[smallest]])
                        smallest = left;
                    if (right < count && costs[heap[right]] < costs[heap[smallest]])
                        smallest = right;
                    if (smallest == i)
                        break;
                    int swap = heap[smallest];
                    heap[smallest] = heap[i];
                    heap[i] = swap;
                    i = smallest;
                }
                return top;
            }
        }
    }
}
