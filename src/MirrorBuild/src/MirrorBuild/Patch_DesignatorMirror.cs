using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MirrorBuild
{
    public static class MirrorPlacer
    {
        public static bool IsMirrorDesignator(Designator des)
        {
            return des is Designator_Place || des is Designator_Plan;
        }
        static bool working;
        static readonly FieldInfo PlacingRotField =
            AccessTools.Field(typeof(Designator_Place), "placingRot")
            ?? AccessTools.Field(typeof(Designator_Build), "placingRot")
            ?? AccessTools.Field(typeof(Designator_Install), "placingRot");

        public static void MirrorFrom(Designator_Place des, IntVec3 c)
        {
            if (working)
                return;
            Map map = des.Map;
            MirrorState state = map?.GetComponent<MirrorState>();
            if (state == null || state.Mode == MirrorMode.Off || PlacingRotField == null)
                return;
            Rot4 rot = (Rot4)PlacingRotField.GetValue(des);
            foreach ((IntVec3 cell, Rot4 r) in MirrorUtility.Mirrors(c, rot, state.Mode, state.AxisX, state.AxisZ))
            {
                if (cell == c || !cell.InBounds(map))
                    continue;
                if (!des.CanDesignateCell(cell).Accepted)
                    continue;
                working = true;
                try
                {
                    PlacingRotField.SetValue(des, r);
                    des.DesignateSingleCell(cell);
                }
                finally
                {
                    PlacingRotField.SetValue(des, rot);
                    working = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Designator_Build), "DesignateSingleCell")]
    public static class Patch_BuildMirror
    {
        public static void Postfix(Designator_Build __instance, IntVec3 c)
        {
            MirrorPlacer.MirrorFrom(__instance, c);
        }
    }

    [HarmonyPatch(typeof(Designator_Install), "DesignateSingleCell")]
    public static class Patch_InstallMirror
    {
        public static void Postfix(Designator_Install __instance, IntVec3 c)
        {
            MirrorPlacer.MirrorFrom(__instance, c);
        }
    }

    [HarmonyPatch(typeof(Designator_Plan_Add), "PlanCells")]
    public static class Patch_PlanMirror
    {
        public static void Prefix(Designator_Plan_Add __instance, ref IEnumerable<IntVec3> cells)
        {
            if (MirrorBuild_Mod.Settings != null && !MirrorBuild_Mod.Settings.MirrorPlans)
                return;
            if (cells == null)
                return;
            Map map = __instance.Map;
            MirrorState state = map?.GetComponent<MirrorState>();
            if (state == null || state.Mode == MirrorMode.Off)
                return;
            state.EnsureAxis();
            List<IntVec3> result = new List<IntVec3>(cells);
            foreach (IntVec3 c in cells.ToList())
            {
                foreach (IntVec3 m in MirrorUtility.MirrorCells(c, state.Mode, state.AxisX, state.AxisZ))
                {
                    if (m.InBounds(map) && !result.Contains(m))
                        result.Add(m);
                }
            }
            cells = result;
        }
    }
}
