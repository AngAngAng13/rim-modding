using CombatExtended;
using HarmonyLib;
using Verse;

namespace FirefoamThrower
{
    [HarmonyPatch(typeof(Verb_ShootCEOneUse), "SelfConsume")]
    public static class Patch_ThrowConsume_OneUse
    {
        public static bool Prefix(Verb_ShootCEOneUse __instance)
        {
            return Patch_ThrowConsume.ShouldSkip(__instance);
        }
    }

    [HarmonyPatch(typeof(Verb_ThrowGrenade), "SelfConsume")]
    public static class Patch_ThrowConsume
    {
        private static JobDef throwDef;

        public static bool Prefix(Verb_ThrowGrenade __instance)
        {
            return ShouldSkip(__instance);
        }

        public static bool ShouldSkip(Verb verb)
        {
            Pawn shooter = verb.CasterPawn;
            if (throwDef == null)
            {
                throwDef = DefDatabase<JobDef>.GetNamed("ThrowFirefoamGrenade", false);
            }
            if (shooter == null || throwDef == null || shooter.jobs == null || shooter.jobs.curJob == null || shooter.jobs.curJob.def != throwDef)
            {
                return true;
            }
            ThingWithComps source = verb.EquipmentSource;
            if (source != null && !source.Destroyed)
            {
                source.Destroy(DestroyMode.Vanish);
            }
            return false;
        }
    }
}
