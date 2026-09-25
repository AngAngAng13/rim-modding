using HarmonyLib;
using RimWorld;
using Verse;

namespace AvoidMechanoidSensors
{
    [HarmonyPatch(typeof(CompSendSignalOnMotion), "Trigger")]
    public static class ActivatorPatch
    {
        static void Postfix(CompSendSignalOnMotion __instance)
        {
            Map map = __instance.parent.Map;
            if (map != null)
                map.GetComponent<DangerGridCache>()?.Notify_ActivatorTriggered();
        }
    }
}
