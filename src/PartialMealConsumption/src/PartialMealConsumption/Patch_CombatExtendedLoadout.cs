using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace PartialMealConsumption
{
    public static class Patch_CombatExtendedLoadout
    {
        public static void Apply(Harmony harmony)
        {
            Type loadoutType = AccessTools.TypeByName("CombatExtended.JobGiver_UpdateLoadout");
            MethodInfo findPickup = loadoutType == null ? null : AccessTools.Method(loadoutType, "FindPickup");
            if (findPickup == null)
                return;

            harmony.Patch(
                findPickup,
                postfix: new HarmonyMethod(typeof(Patch_CombatExtendedLoadout), nameof(RejectPartialMeal)));
        }

        public static void RejectPartialMeal(ref Thing curThing)
        {
            if (PartialMealUtility.GetMealComp(curThing)?.IsPartial == true)
                curThing = null;
        }
    }
}
