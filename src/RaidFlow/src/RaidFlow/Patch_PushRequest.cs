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
            return !FlowFieldRouter.TryResolve(request);
        }
    }
}
