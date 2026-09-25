using HarmonyLib;
using Verse;

namespace RaidFlow
{
    [StaticConstructorOnStartup]
    public static class RaidFlowBootstrap
    {
        static RaidFlowBootstrap()
        {
            var harmony = new Harmony(RaidFlowMod.HarmonyId);
            harmony.PatchAll();
        }
    }
}
