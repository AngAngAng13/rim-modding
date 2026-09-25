using Unity.Collections;
using Verse;

namespace AvoidMechanoidSensors
{
    public class TraderPathCustomizer : PathRequest.IPathGridCustomizer
    {
        private NativeArray<ushort> grid;

        public TraderPathCustomizer(NativeArray<ushort> grid)
        {
            this.grid = grid;
        }

        public NativeArray<ushort> GetOffsetGrid() => grid;
    }
}
