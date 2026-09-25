using System.Collections.Generic;
using Verse;

namespace NoLoot
{
    public class NoLootSettings : ModSettings
    {
        public bool modEnabled = true;
        public HashSet<string> exemptedFactionDefs = new HashSet<string>();

        public bool IsPawnExempted(Pawn pawn)
        {
            if (pawn == null || pawn.Faction == null || exemptedFactionDefs.Count == 0) return false;

            string defName = pawn.Faction.def?.defName;
            return defName != null && exemptedFactionDefs.Contains(defName);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Collections.Look(ref exemptedFactionDefs, "exemptedFactionDefs", LookMode.Value);
            if (exemptedFactionDefs == null)
                exemptedFactionDefs = new HashSet<string>();
        }
    }
}
