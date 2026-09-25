using HarmonyLib;
using Verse;

namespace RaidFlow
{
    [StaticConstructorOnStartup]
    public static class RaidFlowMod
    {
        public const string HarmonyId = "lemonade.raidflow";

        static RaidFlowMod()
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
        }
    }
}
