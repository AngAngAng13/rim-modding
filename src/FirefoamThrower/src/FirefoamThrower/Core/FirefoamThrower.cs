using System;
using HarmonyLib;
using Verse;

namespace FirefoamThrower
{
    [StaticConstructorOnStartup]
    public static class FirefoamThrower
    {
        public const string HarmonyId = "pineapplelemonade67.firefoamthrower";

        static FirefoamThrower()
        {
            try
            {
                if (!ModsConfig.IsActive("CETeam.CombatExtended"))
                {
                    return;
                }
                new Harmony(HarmonyId).PatchAll();
            }
            catch (Exception e)
            {
                Log.Error("[FirefoamThrower] Failed to apply patches: " + e);
            }
        }
    }
}
