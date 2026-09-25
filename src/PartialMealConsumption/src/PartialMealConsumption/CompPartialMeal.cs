using RimWorld;
using UnityEngine;
using Verse;

namespace PartialMealConsumption
{
    public class CompPartialMeal : ThingComp
    {
        private const float ValueEpsilon = 0.0001f;

        private float? remainingNutrition;

        public float FullNutrition => parent.GetStatValue(StatDefOf.Nutrition);

        public float RemainingNutrition
        {
            get
            {
                float fullNutrition = FullNutrition;
                if (!remainingNutrition.HasValue)
                    return fullNutrition;

                return Mathf.Clamp(remainingNutrition.Value, 0f, fullNutrition);
            }
        }

        public float RemainingFraction
        {
            get
            {
                float fullNutrition = FullNutrition;
                if (fullNutrition <= ValueEpsilon)
                    return 0f;

                return RemainingNutrition / fullNutrition;
            }
        }

        public bool IsPartial => RemainingNutrition < FullNutrition - ValueEpsilon;

        public void ConsumeFraction(float fraction)
        {
            if (fraction <= 0f)
                return;

            SetRemainingNutrition(RemainingNutrition - FullNutrition * fraction);
        }

        public void SetRemainingNutrition(float value)
        {
            float fullNutrition = FullNutrition;
            if (value >= fullNutrition - ValueEpsilon)
            {
                remainingNutrition = null;
                return;
            }

            remainingNutrition = Mathf.Clamp(value, 0f, fullNutrition);
        }

        public void RemoveIfEmpty()
        {
            if (RemainingNutrition <= ValueEpsilon && !parent.Destroyed)
                parent.Destroy();
        }

        public override bool AllowStackWith(Thing other)
        {
            if (!base.AllowStackWith(other))
                return false;

            CompPartialMeal otherComp = other.TryGetComp<CompPartialMeal>();
            return otherComp != null && Mathf.Abs(RemainingNutrition - otherComp.RemainingNutrition) <= ValueEpsilon;
        }

        public override void PostSplitOff(Thing piece)
        {
            base.PostSplitOff(piece);

            if (piece == parent)
                return;

            CompPartialMeal pieceComp = piece.TryGetComp<CompPartialMeal>();
            if (pieceComp != null)
            {
                pieceComp.remainingNutrition = remainingNutrition;
                PartialMealLogger.Debug("stack", "SPLIT food=" + parent.ThingID + " piece=" + piece.ThingID + " value=" + RemainingNutrition.ToString("0.###"));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref remainingNutrition, "remainingNutrition", null);
        }

        public override string CompInspectStringExtra()
        {
            float fullNutrition = FullNutrition;
            if (fullNutrition <= ValueEpsilon)
                return null;

            float remaining = RemainingNutrition;
            string text = "Meal nutrition: " + remaining.ToString("0.##") + " / " + fullNutrition.ToString("0.##");
            if (parent.stackCount > 1)
                text += "\nStack nutrition: " + (remaining * parent.stackCount).ToString("0.##");
            return text;
        }

    }
}
