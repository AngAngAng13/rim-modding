using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AvoidMechanoidSensors
{
    [HarmonyPatch]
    public static class WanderPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(RCellFinder), "CanWanderToCell");
        }

        static bool Prefix(IntVec3 c, Pawn pawn, ref bool __result)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
                return true;

            var cache = pawn.Map?.GetComponent<DangerGridCache>();
            if (cache == null || !cache.IsDangerCell(c))
                return true;

            __result = false;
            return false;
        }
    }
}
