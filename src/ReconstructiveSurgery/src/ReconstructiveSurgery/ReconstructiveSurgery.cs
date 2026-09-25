using HarmonyLib;
using Verse;

namespace ReconstructiveSurgery
{
    [StaticConstructorOnStartup]
    public static class ReconstructiveSurgery
    {
        static ReconstructiveSurgery()
        {
            new Harmony("pineapplelemonade67.reconstructivesurgery").PatchAll();
        }
    }
}
