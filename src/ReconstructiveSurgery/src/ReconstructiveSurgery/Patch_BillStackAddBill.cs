using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReconstructiveSurgery
{
    [HarmonyPatch(typeof(BillStack), "AddBill")]
    public static class Patch_BillStackAddBill
    {
        private static RecipeDef masterDef;
        private static RecipeDef reviseDef;
        private static RecipeDef graftDef;
        private static RecipeDef malunionDef;
        private static RecipeDef faceDef;
        private static RecipeDef organDef;
        private static bool resolved;

        public static void Postfix(Bill bill)
        {
            Bill_Medical medical = bill as Bill_Medical;
            if (medical == null)
            {
                return;
            }
            if (!resolved)
            {
                ResolveDefs();
            }
            if (masterDef == null || medical.recipe != masterDef)
            {
                return;
            }
            BillStack stack = bill.billStack;
            if (stack == null)
            {
                return;
            }
            Pawn pawn = stack.billGiver as Pawn;
            if (pawn == null)
            {
                return;
            }
            int added = 0;
            List<BodyPartRecord> parts = new List<BodyPartRecord>(pawn.health.hediffSet.GetNotMissingParts());
            foreach (BodyPartRecord part in parts)
            {
                if (part.depth == BodyPartDepth.Inside)
                {
                    continue;
                }
                added += QueueBillsForPart(stack, pawn, part);
            }
            foreach (BodyPartRecord part in parts)
            {
                if (part.depth != BodyPartDepth.Inside)
                {
                    continue;
                }
                added += QueueBillsForPart(stack, pawn, part);
            }
            if (added > 0)
            {
                stack.Delete(bill);
                return;
            }
            stack.Delete(bill);
            Messages.Message("Full reconstruction needs scar removal research first.", pawn, MessageTypeDefOf.NeutralEvent);
        }

        private static void ResolveDefs()
        {
            resolved = true;
            masterDef = DefDatabase<RecipeDef>.GetNamed("FullReconstruction", false);
            reviseDef = DefDatabase<RecipeDef>.GetNamed("ReviseScar", false);
            graftDef = DefDatabase<RecipeDef>.GetNamed("GraftBurnScar", false);
            malunionDef = DefDatabase<RecipeDef>.GetNamed("CorrectMalunion", false);
            faceDef = DefDatabase<RecipeDef>.GetNamed("ReconstructFace", false);
            organDef = DefDatabase<RecipeDef>.GetNamed("RepairOrganScarring", false);
        }

        private static int QueueBillsForPart(BillStack stack, Pawn pawn, BodyPartRecord part)
        {
            int added = 0;
            foreach (KeyValuePair<RecipeDef, int> entry in RecipeCountsForPart(pawn, part))
            {
                for (int i = 0; i < entry.Value; i++)
                {
                    Bill_Medical childBill = new Bill_Medical(entry.Key, null);
                    stack.AddBill(childBill);
                    childBill.Part = part;
                    added++;
                }
            }
            return added;
        }

        private static Dictionary<RecipeDef, int> RecipeCountsForPart(Pawn pawn, BodyPartRecord part)
        {
            Dictionary<RecipeDef, int> counts = new Dictionary<RecipeDef, int>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h.Part != part || !(h is Hediff_Injury) || !h.IsPermanent() || !h.Visible)
                {
                    continue;
                }
                RecipeDef child = RecipeForScar(part, h.def.defName);
                if (child == null || !ResearchSatisfied(child))
                {
                    continue;
                }
                if (counts.ContainsKey(child))
                {
                    counts[child]++;
                }
                else
                {
                    counts[child] = 1;
                }
            }
            return counts;
        }

        private static RecipeDef RecipeForScar(BodyPartRecord part, string injuryDefName)
        {
            if (Allows(malunionDef, injuryDefName))
            {
                return malunionDef;
            }
            if (part.depth == BodyPartDepth.Inside)
            {
                return Allows(organDef, injuryDefName) ? organDef : null;
            }
            if (part.groups != null && part.groups.Contains(BodyPartGroupDefOf.FullHead))
            {
                if (Allows(faceDef, injuryDefName))
                {
                    return faceDef;
                }
            }
            if (Allows(reviseDef, injuryDefName))
            {
                return reviseDef;
            }
            if (Allows(graftDef, injuryDefName))
            {
                return graftDef;
            }
            return null;
        }

        private static bool ResearchSatisfied(RecipeDef recipe)
        {
            ResearchProjectDef prereq = recipe.researchPrerequisite;
            return prereq == null || prereq.IsFinished;
        }

        private static bool Allows(RecipeDef recipe, string injuryDefName)
        {
            if (recipe == null)
            {
                return false;
            }
            SurgeryScarExtension ext = recipe.GetModExtension<SurgeryScarExtension>();
            return ext != null && ext.allowedInjuryDefs != null && ext.allowedInjuryDefs.Contains(injuryDefName);
        }
    }
}
