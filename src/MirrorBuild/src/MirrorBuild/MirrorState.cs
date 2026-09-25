using RimWorld;
using UnityEngine;
using Verse;

namespace MirrorBuild
{
    public class MirrorState : MapComponent
    {
        public MirrorMode Mode;
        public bool AxisLocked;
        public int AxisX = -1;
        public int AxisZ = -1;

        static KeyBindingDef cycleKey;
        static KeyBindingDef pinKey;

        public MirrorState(Map map) : base(map)
        {
        }

        public void EnsureAxis()
        {
            if (AxisX < 0 || AxisZ < 0)
            {
                AxisX = map.Size.x / 2;
                AxisZ = map.Size.z / 2;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Mode, "mode", MirrorMode.Off);
            Scribe_Values.Look(ref AxisLocked, "axisLocked", false);
            Scribe_Values.Look(ref AxisX, "axisX", -1);
            Scribe_Values.Look(ref AxisZ, "axisZ", -1);
        }

        public override void MapComponentUpdate()
        {
            if (cycleKey == null)
            {
                cycleKey = DefDatabase<KeyBindingDef>.GetNamed("MirrorBuild_CycleMode", false);
                pinKey = DefDatabase<KeyBindingDef>.GetNamed("MirrorBuild_PinAxis", false);
                Log.Message("[MirrorBuild] keys resolved: cycle=" + (cycleKey != null) + " pin=" + (pinKey != null));
            }
            if (cycleKey == null)
                return;
            if (!MirrorPlacer.IsMirrorDesignator(Find.DesignatorManager.SelectedDesignator))
                return;
            if (Find.WindowStack.AnyWindowAbsorbingAllInput)
                return;
            if (cycleKey.JustPressed)
            {
                Mode = (MirrorMode)(((int)Mode + 1) % 4);
                Messages.Message("Mirror mode: " + Mode, MessageTypeDefOf.NeutralEvent, false);
            }
            else if (pinKey != null && pinKey.JustPressed)
            {
                EnsureAxis();
                AxisLocked = !AxisLocked;
                IntVec3 mouse = UI.MouseCell();
                if (AxisLocked && mouse.InBounds(map))
                {
                    AxisX = mouse.x;
                    AxisZ = mouse.z;
                }
                Messages.Message(AxisLocked ? "Mirror axis locked at " + AxisX + ", " + AxisZ : "Mirror axis following mouse", MessageTypeDefOf.NeutralEvent, false);
            }
            if (!AxisLocked)
            {
                IntVec3 mouse = UI.MouseCell();
                if (mouse.InBounds(map))
                {
                    AxisX = mouse.x;
                    AxisZ = mouse.z;
                }
            }
        }

        public override void MapComponentDraw()
        {
            if (Mode == MirrorMode.Off)
                return;
            if (!MirrorPlacer.IsMirrorDesignator(Find.DesignatorManager.SelectedDesignator))
                return;
            EnsureAxis();
            SimpleColor lineColor = MirrorBuild_Mod.Settings != null ? MirrorBuild_Mod.Settings.LineColor : SimpleColor.Cyan;
            float y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
            if (Mode == MirrorMode.Vertical || Mode == MirrorMode.FourWay)
            {
                float x = AxisX + 0.5f;
                GenDraw.DrawLineBetween(new Vector3(x, y, 0f), new Vector3(x, y, map.Size.z), lineColor, 0.3f);
            }
            if (Mode == MirrorMode.Horizontal || Mode == MirrorMode.FourWay)
            {
                float z = AxisZ + 0.5f;
                GenDraw.DrawLineBetween(new Vector3(0f, y, z), new Vector3(map.Size.x, y, z), lineColor, 0.3f);
            }
        }

        public void DrawNumbers()
        {
            if (Mode == MirrorMode.Off)
                return;
            var settings = MirrorBuild_Mod.Settings;
            if (settings != null && !settings.ShowNumbers)
                return;
            int step = settings != null ? Mathf.Clamp(settings.NumberStep, 1, 50) : 5;
            EnsureAxis();
            Color textColor = (settings != null ? settings.LineColor : SimpleColor.Cyan).ToUnityColor();
            if (Mode == MirrorMode.Vertical || Mode == MirrorMode.FourWay)
            {
                float x = AxisX + 0.5f;
                int start = -AxisZ + ((step - (-AxisZ) % step) % step);
                for (int off = start; off < map.Size.z - AxisZ; off += step)
                {
                    if (off == 0)
                        continue;
                    GenMapUI.DrawText(new Vector2(x, AxisZ + off + 0.5f), off.ToString(), textColor);
                }
            }
            if (Mode == MirrorMode.Horizontal || Mode == MirrorMode.FourWay)
            {
                float z = AxisZ + 0.5f;
                int start = -AxisX + ((step - (-AxisX) % step) % step);
                for (int off = start; off < map.Size.x - AxisX; off += step)
                {
                    if (off == 0)
                        continue;
                    GenMapUI.DrawText(new Vector2(AxisX + off + 0.5f, z), off.ToString(), textColor);
                }
            }
        }
    }
}
