using System.Reflection;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace AutoFireTracking
{
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "TryCastShot")]
    public static class Patch_TurretSpriteSync
    {
        private static readonly FieldInfo f_currentTarget = AccessTools.Field(typeof(Verb), "currentTarget");

        static void Postfix(Verb_LaunchProjectileCE __instance)
        {
            Thing caster = __instance.Caster;
            if (!(caster is Building_TurretGunCE turret))
                return;

            LocalTargetInfo ct = (LocalTargetInfo)f_currentTarget.GetValue(__instance);
            if (!ct.IsValid)
                return;

            float angle = Vector3Utility.AngleFlat(
                ct.Cell.ToVector3Shifted() - caster.DrawPos);

            turret.top.CurRotation = angle;

            if (turret.NonSnap)
                turret.NonSnapTurretRot = angle;
        }
    }
}
