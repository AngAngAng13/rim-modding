using UnityEngine;
using Verse;

namespace MirrorBuild
{
    public class MirrorBuildSettings : ModSettings
    {
        public SimpleColor LineColor = SimpleColor.Cyan;
        public bool ShowNumbers = true;
        public int NumberStep = 5;
        public bool MirrorPlans;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref LineColor, "lineColor", SimpleColor.Cyan);
            Scribe_Values.Look(ref ShowNumbers, "showNumbers", true);
            Scribe_Values.Look(ref NumberStep, "numberStep", 5);
            Scribe_Values.Look(ref MirrorPlans, "mirrorPlans", false);
        }
    }

    public class MirrorBuild_Mod : Mod
    {
        public static MirrorBuildSettings Settings;

        string numberStepBuffer;

        static readonly SimpleColor[] Colors =
        {
            SimpleColor.White, SimpleColor.Red, SimpleColor.Green, SimpleColor.Blue,
            SimpleColor.Magenta, SimpleColor.Yellow, SimpleColor.Cyan, SimpleColor.Orange
        };

        public MirrorBuild_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MirrorBuildSettings>();
        }

        public override string SettingsCategory() => "Mirror Build";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Show axis numbers", ref Settings.ShowNumbers);
            list.Label("Number gap in cells, counted outward from the axis:");
            list.IntEntry(ref Settings.NumberStep, ref numberStepBuffer, 1);
            if (Settings.NumberStep < 1)
                Settings.NumberStep = 1;
            if (Settings.NumberStep > 50)
                Settings.NumberStep = 50;
            list.CheckboxLabeled("Mirror planning designations", ref Settings.MirrorPlans);
            list.Label("Axis line color (pick whatever you can see best):");
            foreach (SimpleColor color in Colors)
            {
                if (list.RadioButton(color.ToString(), Settings.LineColor == color))
                    Settings.LineColor = color;
            }
            list.GapLine();
            list.Label("Keys (M cycle mode, N lock axis) are rebindable in Options > Keyboard > Architect.");
            list.End();
        }
    }
}
