using System.Diagnostics;
using HarmonyLib;
using Verse;

namespace RaidFlow
{
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.PushRequest))]
    public static class Patch_PushRequest
    {
        static bool Prefix(PathRequest request)
        {
            if (!RaidFlowScope.IsRaidPath(request))
                return true;
            if (RaidFlowMod.Settings == null || RaidFlowMod.Settings.useSharedRoutes)
            {
                long start = Stopwatch.GetTimestamp();
                if (FlowFieldRouter.TryResolve(request))
                {
                    RaidFlowProfiler.RecordShared(RaidFlowProfiler.ElapsedMs(start));
                    return false;
                }
            }
            RaidFlowProfiler.RecordNormalStart(request);
            return true;
        }
    }
}
