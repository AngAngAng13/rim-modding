using CombatExtended;
using HarmonyLib;

namespace AutoFireTracking
{
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "LockRotationAndAngle", MethodType.Getter)]
    public static class Patch_AutoFireTracking
    {
        static void Postfix(ref bool __result, Verb_LaunchProjectileCE __instance)
        {
            if (!__result)
                return;

            var compFireModes = __instance.CompFireModes;
            if (compFireModes == null)
                return;

            if (compFireModes.CurrentFireMode == FireMode.AutoFire)
                __result = false;
        }
    }
}
