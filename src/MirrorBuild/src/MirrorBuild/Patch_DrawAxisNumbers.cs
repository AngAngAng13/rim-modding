using HarmonyLib;
using RimWorld;
using Verse;

namespace MirrorBuild
{
    [HarmonyPatch(typeof(MapInterface), "MapInterfaceOnGUI_AfterMainTabs")]
    public static class Patch_DrawAxisNumbers
    {
        public static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;
            Map map = Find.CurrentMap;
            if (map == null)
                return;
            if (!MirrorPlacer.IsMirrorDesignator(Find.DesignatorManager.SelectedDesignator))
                return;
            map.GetComponent<MirrorState>()?.DrawNumbers();
        }
    }
}
