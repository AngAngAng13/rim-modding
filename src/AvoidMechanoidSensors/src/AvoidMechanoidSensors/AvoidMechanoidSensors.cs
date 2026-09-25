using HarmonyLib;
using Verse;

namespace AvoidMechanoidSensors
{
    [StaticConstructorOnStartup]
    public static class AvoidMechanoidSensorsMod
    {
        static AvoidMechanoidSensorsMod()
        {
            var harmony = new Harmony("pineappleLemonade67.avoidmechanoidsensors");
            harmony.PatchAll();
        }
    }
}
