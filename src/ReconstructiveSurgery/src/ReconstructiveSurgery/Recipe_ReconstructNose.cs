using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ReconstructiveSurgery
{
    public class Recipe_ReconstructNose : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part))
            {
                return false;
            }
            Pawn pawn = thing as Pawn;
            if (pawn == null)
            {
                return false;
            }
            if (part != null)
            {
                return IsMissingNose(pawn, part);
            }
            foreach (BodyPartRecord candidate in CandidateParts(pawn))
            {
                if (IsMissingNose(pawn, candidate))
                {
                    return true;
                }
            }
            return false;
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (!recipe.targetsBodyPart)
            {
                yield break;
            }
            foreach (BodyPartRecord candidate in CandidateParts(pawn))
            {
                if (IsMissingNose(pawn, candidate))
                {
                    yield return candidate;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }
            Hediff missing = null;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_MissingPart && hediffs[i].Part == part)
                {
                    missing = hediffs[i];
                    break;
                }
            }
            if (missing == null)
            {
                return;
            }
            pawn.health.RemoveHediff(missing);
            HediffDef noseDef = DefDatabase<HediffDef>.GetNamed("ReconstructedNose", false);
            if (noseDef != null)
            {
                pawn.health.AddHediff(noseDef, part, null);
            }
        }

        private IEnumerable<BodyPartRecord> CandidateParts(Pawn pawn)
        {
            if (!recipe.appliedOnFixedBodyParts.NullOrEmpty() || !recipe.appliedOnFixedBodyPartGroups.NullOrEmpty())
            {
                foreach (BodyPartRecord fixedPart in MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn))
                {
                    yield return fixedPart;
                }
                yield break;
            }
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                yield return part;
            }
        }

        private static bool IsMissingNose(Pawn pawn, BodyPartRecord part)
        {
            if (part == null || part.def.defName != "Nose")
            {
                return false;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_MissingPart && hediffs[i].Part == part)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
