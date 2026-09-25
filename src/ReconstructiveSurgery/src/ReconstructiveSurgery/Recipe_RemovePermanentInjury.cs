using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ReconstructiveSurgery
{
    public class Recipe_RemovePermanentInjury : Recipe_Surgery
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
                return HasMatchingScar(pawn, part);
            }
            foreach (BodyPartRecord candidate in CandidateParts(pawn))
            {
                if (HasMatchingScar(pawn, candidate))
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
                if (HasMatchingScar(pawn, candidate))
                {
                    yield return candidate;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff target = WorstMatchingScar(pawn, part);
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                if (PawnUtility.ShouldSendNotificationAbout(pawn) || PawnUtility.ShouldSendNotificationAbout(billDoer))
                {
                    string text;
                    if (!recipe.successfullyRemovedHediffMessage.NullOrEmpty())
                    {
                        text = recipe.successfullyRemovedHediffMessage.Formatted(billDoer.LabelShort, pawn.LabelShort);
                    }
                    else if (target != null)
                    {
                        text = "MessageSuccessfullyRemovedHediff".Translate(billDoer.LabelShort, pawn.LabelShort, target.LabelCap, billDoer.Named("SURGEON"), pawn.Named("PATIENT"));
                    }
                    else
                    {
                        text = billDoer.LabelShort + " has successfully operated on " + pawn.LabelShort + ".";
                    }
                    Messages.Message(text, pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
            if (target != null)
            {
                pawn.health.RemoveHediff(target);
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

        private bool HasMatchingScar(Pawn pawn, BodyPartRecord part)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i].Part == part && IsMatchingScar(hediffs[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private Hediff WorstMatchingScar(Pawn pawn, BodyPartRecord part)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            Hediff worst = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (recipe.targetsBodyPart && (part == null || h.Part != part))
                {
                    continue;
                }
                if (!IsMatchingScar(h))
                {
                    continue;
                }
                if (worst == null || h.Severity > worst.Severity)
                {
                    worst = h;
                }
            }
            return worst;
        }

        private bool IsMatchingScar(Hediff h)
        {
            if (!(h is Hediff_Injury))
            {
                return false;
            }
            if (!h.IsPermanent())
            {
                return false;
            }
            if (!h.Visible)
            {
                return false;
            }
            if (recipe.targetsBodyPart && h.Part == null)
            {
                return false;
            }
            SurgeryScarExtension ext = recipe.GetModExtension<SurgeryScarExtension>();
            if (ext != null && ext.allowedInjuryDefs != null && ext.allowedInjuryDefs.Count > 0)
            {
                if (!ext.allowedInjuryDefs.Contains(h.def.defName))
                {
                    return false;
                }
            }
            bool outsideOnly = ext == null || (ext.requireOutsideOnly && !ext.requireInsideOnly);
            if (outsideOnly && h.Part != null && h.Part.depth == BodyPartDepth.Inside)
            {
                return false;
            }
            if (ext != null && ext.requireInsideOnly && (h.Part == null || h.Part.depth != BodyPartDepth.Inside))
            {
                return false;
            }
            return true;
        }
    }
}
