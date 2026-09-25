using HarmonyLib;
using Verse;

namespace MirrorBuild
{
    [StaticConstructorOnStartup]
    public static class MirrorBuildMod
    {
        static MirrorBuildMod()
        {
            new Harmony("pineapplelemonade67.mirrorbuild").PatchAll();
            Log.Message("[MirrorBuild] loaded, patches applied.");
        }
    }
}
