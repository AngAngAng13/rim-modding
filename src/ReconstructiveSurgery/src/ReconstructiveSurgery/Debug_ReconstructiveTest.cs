#if DEBUG
using System.Linq;
using LudeonTK;
using Verse;

namespace ReconstructiveSurgery
{
    public static class Debug_ReconstructiveTest
    {
        [DebugAction("ReconstructiveSurgery", "Apply test scars", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ApplyTestScars(Pawn p)
        {
            AddScar(p, "Cut", "Arm", 12f);
            AddScar(p, "Stab", "Hand", 10f);
            AddScar(p, "Burn", "Torso", 12f);
            AddScar(p, "Cut", "Torso", 9f);
            AddScar(p, "Shredded", "Torso", 11f);
            AddScar(p, "Frostbite", "Foot", 10f);
            AddScar(p, "Crack", "Femur", 8f);
            AddScar(p, "Cut", "Nose", 6f);
            AddScar(p, "Crack", "Jaw", 8f);
            AddScar(p, "Crush", "Leg", 12f);
            AddScar(p, "Gunshot", "Shoulder", 10f);
            AddScar(p, "Gunshot", "Lung", 10f);
        }

        [DebugAction("ReconstructiveSurgery", "Remove test nose", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RemoveTestNose(Pawn p)
        {
            BodyPartRecord nose = p.RaceProps.body.AllParts.FirstOrDefault(r => r.def.defName == "Nose");
            if (nose == null || !p.health.hediffSet.GetNotMissingParts().Contains(nose))
            {
                return;
            }
            HediffDef missing = DefDatabase<HediffDef>.GetNamed("MissingBodyPart", false);
            if (missing == null)
            {
                return;
            }
            p.health.AddHediff(missing, nose);
        }

        [DebugAction("ReconstructiveSurgery", "Clear test scars", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ClearTestScars(Pawn p)
        {
            for (int i = p.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
            {
                Hediff h = p.health.hediffSet.hediffs[i];
                if (h is Hediff_Injury && h.IsPermanent())
                {
                    p.health.RemoveHediff(h);
                }
            }
        }

        private static void AddScar(Pawn pawn, string injuryDefName, string partDefName, float severity)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(injuryDefName, false);
            if (def == null)
            {
                return;
            }
            BodyPartRecord part = pawn.RaceProps.body.AllParts.FirstOrDefault(r => r.def.defName == partDefName);
            if (part == null)
            {
                return;
            }
            if (!pawn.health.hediffSet.GetNotMissingParts().Contains(part))
            {
                return;
            }
            Hediff h = pawn.health.AddHediff(def, part);
            h.Severity = severity;
            HediffComp_GetsPermanent comp = h.TryGetComp<HediffComp_GetsPermanent>();
            if (comp != null)
            {
                comp.IsPermanent = true;
            }
        }
    }
}
#endif
