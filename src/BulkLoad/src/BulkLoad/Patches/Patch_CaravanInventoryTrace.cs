#if DEBUG
using System;
using System.Collections.Generic;
#endif
using HarmonyLib;
using RimWorld;
#if DEBUG
using RimWorld.Planet;
#endif
using Verse;
using Verse.AI;

namespace BulkLoad
{
#if DEBUG
    [HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
        new[] { typeof(IEnumerable<Pawn>), typeof(Faction), typeof(PlanetTile), typeof(PlanetTile), typeof(PlanetTile), typeof(bool) })]
    internal static class Patch_CaravanDepartTrace
    {
        private static void Prefix(IEnumerable<Pawn> pawns)
        {
            string roster = "";
            foreach (var pawn in pawns)
            {
                if (pawn == null)
                    continue;
                roster += pawn.LabelShort + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]; ";
            }
            BulkLoadDiagnostics.Debug("caravan", "Caravan DEPART " + roster);
        }
    }
#endif

#if DEBUG
    [HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter),
        new[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
    internal static class Patch_CaravanReturnTrace
    {
        private static void Prefix(Caravan caravan, Map map, CaravanDropInventoryMode dropInventoryMode)
        {
            string roster = "";
            var members = caravan?.PawnsListForReading;
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var pawn = members[i];
                    if (pawn == null)
                        continue;
                    roster += pawn.LabelShort + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]; ";
                }
            }
            BulkLoadDiagnostics.Debug(
                "caravan",
                "Caravan RETURN map=" + map?.Parent?.Label + " mode=" + dropInventoryMode + " " + roster);
        }
    }
#endif

#if DEBUG
    [HarmonyPatch(typeof(JobGiver_UnloadYourInventory), "TryGiveJob")]
    internal static class Patch_SelfUnloadGrantTrace
    {
        private static void Postfix(Pawn pawn, Job __result)
        {
            if (__result == null || pawn == null)
                return;
            BulkLoadDiagnostics.Debug(
                "caravan",
                "SelfUnload granted pawn=" + pawn.LabelShort
                + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]");
        }
    }
#endif

    [HarmonyPatch(typeof(JobGiver_UnloadYourInventory), "TryGiveJob")]
    internal static class Patch_SelfUnloadRescue
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null || pawn == null || pawn.inventory == null)
                return;
            if (!BulkLoadToggles.SelfUnloadRescueEnabled)
                return;
            if (!pawn.inventory.UnloadEverything)
                return;
            ThingCount first = pawn.inventory.FirstUnloadableThing;
            if (first == default(ThingCount) || first.Thing == null || first.Thing.Destroyed
                || !pawn.inventory.innerContainer.Contains(first.Thing))
                return;
            __result = JobMaker.MakeJob(BulkLoadDefOf.BulkUnloadOwnInventory);
            BulkLoadDiagnostics.Debug(
                "caravan",
                "SelfUnload rescued pawn=" + pawn.LabelShort
                + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]");
        }
    }

#if DEBUG
    [HarmonyPatch(typeof(Pawn_InventoryTracker), "set_UnloadEverything")]
    internal static class Patch_UnloadEverythingTrace
    {
        private static void Postfix(Pawn_InventoryTracker __instance, bool value)
        {
            var pawn = __instance?.pawn;
            if (pawn == null)
                return;
            BulkLoadDiagnostics.Debug(
                "caravan",
                "UnloadEverything set pawn=" + pawn.LabelShort + " set=" + value
                + " effective=" + __instance.UnloadEverything
                + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]");
        }
    }
#endif
}
