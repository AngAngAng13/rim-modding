using HarmonyLib;
using Verse;
using Verse.AI;

namespace AvoidMechanoidSensors
{
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.CreateRequest))]
    public static class CreateRequestPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(new[] {
            typeof(IntVec3), typeof(LocalTargetInfo), typeof(IntVec3?),
            typeof(Pawn), typeof(PathFinderCostTuning?), typeof(PathEndMode),
            typeof(PathRequest.IPathGridCustomizer)
        })]
        static void Postfix(PathRequest __result, Pawn pawn)
        {
            if (__result.customizer != null)
                return;
            if (pawn == null || !pawn.RaceProps.Humanlike)
                return;

            var customizer = pawn.Map?
                .GetComponent<DangerGridCache>()?.GetCustomizer();
            if (customizer != null)
                __result.customizer = customizer;
        }
    }
}
