using System.Reflection;
using Verse.AI;

namespace RaidFlow
{
    public static class PawnPathInit
    {
        private static readonly FieldInfo nodeIndex = typeof(PawnPath).GetField("curNodeIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo totalCost = typeof(PawnPath).GetField("totalCostInt", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo inUse = typeof(PawnPath).GetField("inUse", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool SetStarted(PawnPath path, int lastIndex, int cost)
        {
            if (nodeIndex == null || totalCost == null || inUse == null)
                return false;
            nodeIndex.SetValue(path, lastIndex);
            totalCost.SetValue(path, (float)cost);
            inUse.SetValue(path, true);
            return true;
        }
    }
}
