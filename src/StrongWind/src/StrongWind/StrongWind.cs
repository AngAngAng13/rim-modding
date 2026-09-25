using HarmonyLib;
using Verse;

namespace StrongWind
{
    [StaticConstructorOnStartup]
    public static class StrongWind
    {
        static StrongWind()
        {
            new Harmony("pineapplelemonade67.strongwind").PatchAll();
        }
    }
}
