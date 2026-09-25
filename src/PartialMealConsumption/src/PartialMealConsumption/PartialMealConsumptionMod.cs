using HarmonyLib;
using Verse;

namespace PartialMealConsumption
{
    [StaticConstructorOnStartup]
    public static class PartialMealConsumptionMod
    {
        static PartialMealConsumptionMod()
        {
            var harmony = new Harmony("pineapplelemonade67.partialmealconsumption");
            harmony.PatchAll();
            Patch_CombatExtendedLoadout.Apply(harmony);
            PartialMealLogger.Initialize();
            InjectFoodComponents();
        }

        private static void InjectFoodComponents()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.category != ThingCategory.Item || !def.IsNutritionGivingIngestible || def.IsDrug || def.ingestible?.IsMeal != true)
                    continue;

                if (!def.HasComp(typeof(CompPartialMeal)))
                    def.comps.Add(new CompProperties(typeof(CompPartialMeal)));
            }
        }
    }
}
