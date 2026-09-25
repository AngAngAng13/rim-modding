using Verse;
using HarmonyLib;

namespace NoLoot
{
    [StaticConstructorOnStartup]
    public static class NoLoot
    {
        public static readonly HediffDef DeathAcidifierDef = DefDatabase<HediffDef>.GetNamed("DeathAcidifier");

        static NoLoot()
        {
            var harmony = new Harmony("com.noloot.rimworld.mod");
            harmony.PatchAll();
        }
    }
}
