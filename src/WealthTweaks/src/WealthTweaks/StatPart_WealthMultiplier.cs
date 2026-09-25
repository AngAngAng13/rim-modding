using RimWorld;
using Verse;

namespace WealthTweaks
{
    public class StatPart_WealthMultiplier : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (WealthTweaksMod.Settings == null)
                return;

            ThingDef def = GetThingDef(req);
            if (def == null)
                return;

            val *= WealthTweaksMod.Settings.GetMultiplier(def.defName);
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (WealthTweaksMod.Settings == null)
                return null;

            ThingDef def = GetThingDef(req);
            if (def == null)
                return null;

            float totalMult = WealthTweaksMod.Settings.GetMultiplier(def.defName);
            if (totalMult == 1f)
                return null;

            string line = "WealthTweaks: " + totalMult.ToString("F2") + "x";
            if (WealthTweaksMod.Settings.GlobalMultiplier != 1f)
            {
                float specific = WealthTweaksMod.Settings.GetSpecificMultiplier(def.defName);
                line += $" (global {WealthTweaksMod.Settings.GlobalMultiplier:F2}x * specific {specific:F2}x)";
            }
            return line;
        }

        private static ThingDef GetThingDef(StatRequest req)
        {
            if (req.HasThing)
                return req.Thing.def;
            return req.Def as ThingDef;
        }
    }
}
