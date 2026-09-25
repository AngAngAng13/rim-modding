using Verse;

namespace StrongWind
{
    public class StrongWindSettings : ModSettings
    {
        public bool enabled = true;
        public bool swirlPawns = true;
        public bool swirlItems = true;
        public bool collisionsEnabled = true;
        public float collisionDamageScale = 1f;
        public float startHeight = 1f;
        public float maxHeight = 10f;
        public float startRadius = 4f;
        public float endRadius = 9f;
        public int spinDurationMinTicks = 180;
        public int spinDurationMaxTicks = 420;
        public float angularSpeed = 5f;
        public float grabChance = 0.25f;
        public int maxActiveSwirls = 5;
        public int stunMinTicks = 60;
        public int stunMaxTicks = 180;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref swirlPawns, "swirlPawns", true);
            Scribe_Values.Look(ref swirlItems, "swirlItems", true);
            Scribe_Values.Look(ref collisionsEnabled, "collisionsEnabled", true);
            Scribe_Values.Look(ref collisionDamageScale, "collisionDamageScale", 1f);
            Scribe_Values.Look(ref startHeight, "startHeight", 1f);
            Scribe_Values.Look(ref maxHeight, "maxHeight", 10f);
            Scribe_Values.Look(ref startRadius, "startRadius", 4f);
            Scribe_Values.Look(ref endRadius, "endRadius", 9f);
            Scribe_Values.Look(ref spinDurationMinTicks, "spinDurationMinTicks", 180);
            Scribe_Values.Look(ref spinDurationMaxTicks, "spinDurationMaxTicks", 420);
            Scribe_Values.Look(ref angularSpeed, "angularSpeed", 5f);
            Scribe_Values.Look(ref grabChance, "grabChance", 0.25f);
            Scribe_Values.Look(ref maxActiveSwirls, "maxActiveSwirls", 5);
            Scribe_Values.Look(ref stunMinTicks, "stunMinTicks", 60);
            Scribe_Values.Look(ref stunMaxTicks, "stunMaxTicks", 180);
        }
    }
}
