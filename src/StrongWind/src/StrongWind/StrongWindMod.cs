using UnityEngine;
using Verse;

namespace StrongWind
{
    public class StrongWindMod : Mod
    {
        public static StrongWindSettings Settings;

        public StrongWindMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<StrongWindSettings>();
        }

        public override string SettingsCategory()
        {
            return "Strong Wind";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Swirl pawns around tornadoes", ref Settings.enabled);
            listing.CheckboxLabeled("Grab pawns", ref Settings.swirlPawns);
            listing.CheckboxLabeled("Grab loose items and stone chunks", ref Settings.swirlItems);
            listing.CheckboxLabeled("Mid-air collisions hurt", ref Settings.collisionsEnabled);
            listing.Label("Collision damage: " + (Settings.collisionDamageScale * 100f).ToString("0") + "%");
            Settings.collisionDamageScale = listing.Slider(Settings.collisionDamageScale, 0f, 3f);
            listing.Gap();
            listing.Label("Ride height in cells");
            FloatRange heightRange = new FloatRange(Settings.startHeight, Settings.maxHeight);
            Widgets.FloatRange(listing.GetRect(40f), 101, ref heightRange, 0f, 30f, null, ToStringStyle.Integer);
            Settings.startHeight = heightRange.min;
            Settings.maxHeight = heightRange.max;
            listing.Label("Funnel radius in cells");
            FloatRange radiusRange = new FloatRange(Settings.startRadius, Settings.endRadius);
            Widgets.FloatRange(listing.GetRect(40f), 102, ref radiusRange, 1f, 15f, null, ToStringStyle.Integer);
            Settings.startRadius = radiusRange.min;
            Settings.endRadius = radiusRange.max;
            listing.Label("Landing stun in seconds");
            FloatRange stunRange = new FloatRange(Settings.stunMinTicks / 60f, Settings.stunMaxTicks / 60f);
            Widgets.FloatRange(listing.GetRect(40f), 103, ref stunRange, 0f, 10f, null, ToStringStyle.FloatOne);
            Settings.stunMinTicks = (int)(stunRange.min * 60f);
            Settings.stunMaxTicks = (int)(stunRange.max * 60f);
            listing.Label("Ride time in seconds");
            FloatRange durationRange = new FloatRange(Settings.spinDurationMinTicks / 60f, Settings.spinDurationMaxTicks / 60f);
            Widgets.FloatRange(listing.GetRect(40f), 104, ref durationRange, 1f, 20f, null, ToStringStyle.FloatOne);
            Settings.spinDurationMinTicks = (int)(durationRange.min * 60f);
            Settings.spinDurationMaxTicks = (int)(durationRange.max * 60f);
            listing.Label("Spin speed: " + Settings.angularSpeed.ToString("0") + " degrees per tick");
            Settings.angularSpeed = listing.Slider(Settings.angularSpeed, 1f, 12f);
            listing.Label("Grab chance per hit: " + (Settings.grabChance * 100f).ToString("0") + "%");
            Settings.grabChance = listing.Slider(Settings.grabChance, 0f, 1f);
            listing.Label("Max victims swirling at once: " + Settings.maxActiveSwirls);
            Settings.maxActiveSwirls = (int)listing.Slider(Settings.maxActiveSwirls, 1f, 100f);
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
