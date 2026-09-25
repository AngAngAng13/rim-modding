using HarmonyLib;
using Verse;

namespace AutoFireTracking
{
    [StaticConstructorOnStartup]
    public static class AutoFireTracking
    {
        static AutoFireTracking()
        {
            var harmony = new Harmony("pineapplelemonade67.autofiretracking");
            harmony.PatchAll();
        }
    }
}
