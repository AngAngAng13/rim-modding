using HarmonyLib;
using Verse;

namespace RaidFlow
{
    [HarmonyPatch(typeof(PathRequest), nameof(PathRequest.Resolve))]
    public static class Patch_PathRequestResolve
    {
        static void Postfix(PathRequest __instance)
        {
            RaidFlowProfiler.FinishNormal(__instance);
        }
    }
}
