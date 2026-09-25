using Unity.Collections;
using Verse;
using RimWorld;

namespace AvoidMechanoidSensors
{
    public class DangerGridCache : MapComponent
    {
        private NativeArray<ushort> dangerGrid;
        private TraderPathCustomizer customizer;
        private bool isDirty = true;
        private bool hasDanger;

        private const ushort DangerCost = 5000;

        public DangerGridCache(Map map) : base(map)
        {
            dangerGrid = new NativeArray<ushort>(
                map.cellIndices.NumGridCells, Allocator.Persistent);

            map.events.BuildingSpawned += OnBuildingChanged;
            map.events.BuildingDespawned += OnBuildingChanged;
        }

        private void OnBuildingChanged(Building b)
        {
            if (b.def == ThingDefOf.ActivatorProximity)
                isDirty = true;
        }

        public void Notify_ActivatorTriggered()
        {
            isDirty = true;
        }

        public TraderPathCustomizer GetCustomizer()
        {
            if (isDirty)
                Rebuild();
            return customizer;
        }

        public bool IsDangerCell(IntVec3 cell)
        {
            if (!cell.InBounds(map))
                return false;
            if (isDirty)
                Rebuild();
            return dangerGrid[map.cellIndices.CellToIndex(cell)] > 0;
        }

        private void Rebuild()
        {
            isDirty = false;
            hasDanger = false;

            for (int i = 0; i < dangerGrid.Length; i++)
                dangerGrid[i] = 0;

            ThingDef sensorDef = ThingDefOf.ActivatorProximity;
            if (sensorDef == null)
            {
                customizer = null;
                return;
            }

            foreach (Thing t in map.listerThings
                .ThingsOfDef(sensorDef))
            {
                var motionComp = t.TryGetComp<CompSendSignalOnMotion>();
                if (motionComp == null || motionComp.Sent)
                    continue;

                float radius = motionComp.Props.radius;

                foreach (IntVec3 cell in GenRadial
                    .RadialCellsAround(t.Position, radius, true))
                {
                    if (cell.InBounds(map))
                    {
                        dangerGrid[map.cellIndices.CellToIndex(cell)] = DangerCost;
                        hasDanger = true;
                    }
                }
            }

            customizer = hasDanger ? new TraderPathCustomizer(dangerGrid) : null;
        }

        public override void FinalizeInit()
        {
            Rebuild();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                isDirty = true;
        }

    }
}
