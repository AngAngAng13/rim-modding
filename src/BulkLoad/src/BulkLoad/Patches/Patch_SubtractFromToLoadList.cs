#if DEBUG
using HarmonyLib;
using RimWorld;
using Verse;
#endif

namespace BulkLoad
{
#if DEBUG
    [HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.SubtractFromToLoadList))]
    public static class Patch_SubtractFromToLoadList
    {
        static void Postfix(CompTransporter __instance, Thing t, int count, int __result)
        {
            BulkLoadDiagnostics.Debug(
                "demand",
                "Subtract pod=" + (__instance?.parent?.thingIDNumber ?? -1)
                + " def=" + t?.def?.defName
                + " thingStack=" + (t?.stackCount ?? -1)
                + " countArg=" + count
                + " subtracted=" + __result);
        }
    }
#endif
}
