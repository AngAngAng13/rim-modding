using Verse;

namespace PartialMealConsumption
{
    public static class PartialMealUtility
    {
        public static CompPartialMeal GetMealComp(Thing thing)
        {
            if (thing?.def?.ingestible?.IsMeal != true)
                return null;

            return thing.TryGetComp<CompPartialMeal>();
        }
    }
}
