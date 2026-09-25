using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PartialMealConsumption
{
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.ChewIngestible))]
    public static class Patch_ChewIngestible
    {
        public static void Postfix(Toil __result, Pawn chewer, float durationMultiplier, TargetIndex ingestibleInd)
        {
            PartialMealEatState state = new PartialMealEatState(__result, chewer, durationMultiplier, ingestibleInd);

            Action vanillaInitAction = __result.initAction;
            __result.initAction = () =>
            {
                vanillaInitAction?.Invoke();
                state.Begin();
            };

            Action<int> vanillaTickAction = __result.tickIntervalAction;
            __result.tickIntervalAction = delta =>
            {
                vanillaTickAction?.Invoke(delta);
                state.Tick(delta);
            };

            __result.AddFinishAction(state.FinishChewing);
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.NutritionForEater))]
    public static class Patch_NutritionForEater
    {
        public static void Postfix(Thing food, ref float __result)
        {
            CompPartialMeal comp = PartialMealUtility.GetMealComp(food);
            if (comp == null || comp.FullNutrition <= 0f)
                return;

            __result *= comp.RemainingFraction;
        }
    }

    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    public static class Patch_FinalizeIngest
    {
        public static void Postfix(Toil __result, Pawn ingester, TargetIndex ingestibleInd)
        {
            Action vanillaInitAction = __result.initAction;
            __result.initAction = () =>
            {
                Job job = __result.actor?.CurJob;
                Thing thing = job?.GetTarget(ingestibleInd).Thing;
                if (!PartialMealEatState.TryFinalize(job, ingester, thing, vanillaInitAction))
                    vanillaInitAction?.Invoke();
            };
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_JobDriverCleanup
    {
        public static void Postfix(JobDriver __instance, JobCondition condition)
        {
            PartialMealEatState.CleanupJob(__instance.job, condition);
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.AddFoodPoisoningHediff))]
    public static class Patch_AddFoodPoisoningHediff
    {
        public static bool Prefix(Pawn pawn, Thing ingestible)
        {
            return !PartialMealEatState.ShouldSuppressPoison(pawn, ingestible);
        }
    }

    internal sealed class PartialMealEatState
    {
        private const float CompletionEpsilon = 0.0001f;

        private static readonly Dictionary<int, PartialMealEatState> ActiveStates = new Dictionary<int, PartialMealEatState>();
        private static readonly HashSet<PoisonKey> ProtectedPoisonChecks = new HashSet<PoisonKey>();

        private readonly Toil toil;
        private readonly Pawn eater;
        private readonly float durationMultiplier;
        private readonly TargetIndex ingestibleInd;

        private Pawn actor;
        private Job job;
        private Thing thing;
        private CompPartialMeal comp;
        private float targetPortions;
        private float appliedPortions;
        private float nutritionPerPortion;
        private float initialRemainingNutrition;
        private int units;
        private int totalTicks;
        private int elapsedTicks;
        private int jobId = -1;
        private bool poisonChecked;
        private bool active;
        private bool chewingFinished;
        private bool finalized;

        public PartialMealEatState(Toil toil, Pawn eater, float durationMultiplier, TargetIndex ingestibleInd)
        {
            this.toil = toil;
            this.eater = eater;
            this.durationMultiplier = durationMultiplier;
            this.ingestibleInd = ingestibleInd;
        }

        public void Begin()
        {
            actor = toil.actor;
            job = actor?.CurJob;
            thing = job?.GetTarget(ingestibleInd).Thing;
            comp = PartialMealUtility.GetMealComp(thing);

            if (actor == null || eater == null || job == null || thing == null || comp == null || !thing.IngestibleNow)
                return;

            if (!eater.RaceProps.Humanlike)
                return;

            if (thing.Spawned)
                return;

            if (actor.carryTracker?.CarriedThing != thing)
                return;

            if (thing.stackCount < 1)
                return;

            units = thing.stackCount;
            initialRemainingNutrition = comp.RemainingNutrition;
            if (initialRemainingNutrition <= CompletionEpsilon)
                return;

            targetPortions = comp.RemainingFraction;
            nutritionPerPortion = FullNutritionForEater(eater, thing);
            totalTicks = Mathf.Max(1, actor.jobs.curDriver.ticksLeftThisToil);
            actor.jobs.curDriver.ticksLeftThisToil = Mathf.Max(1, Mathf.RoundToInt(totalTicks * targetPortions));
            totalTicks = actor.jobs.curDriver.ticksLeftThisToil;
            jobId = job.loadID;
            active = true;
            ActiveStates[jobId] = this;
            PartialMealLogger.Debug("meal", "BEGIN pawn=" + eater.LabelShort + " food=" + thing.ThingID + " value=" + initialRemainingNutrition.ToString("0.###") + " ticks=" + totalTicks);
        }

        public void Tick(int delta)
        {
            if (!active || delta <= 0 || actor == null || actor.CurJob != job)
                return;

            int nextElapsedTicks = Mathf.Min(totalTicks, elapsedTicks + delta);
            float targetAppliedPortions = targetPortions * nextElapsedTicks / totalTicks;
            float portionDelta = targetAppliedPortions - appliedPortions;
            elapsedTicks = nextElapsedTicks;

            if (portionDelta <= CompletionEpsilon)
                return;

            comp.ConsumeFraction(portionDelta);
            appliedPortions += portionDelta;

            if (!eater.Dead && eater.needs?.food != null)
            {
                float nutritionDelta = portionDelta * nutritionPerPortion * units;
                eater.needs.food.CurLevel += nutritionDelta;
                eater.records.AddTo(RecordDefOf.NutritionEaten, nutritionDelta);
                PartialMealLogger.DebugThrottled("meal", "tick-" + jobId, 300, "TICK pawn=" + eater.LabelShort + " food=" + thing.ThingID + " remaining=" + comp.RemainingNutrition.ToString("0.###"));
            }

            if (!poisonChecked)
            {
                poisonChecked = true;
                ApplyEarlyPoisonCheck();
            }
        }

        public void FinishChewing()
        {
            chewingFinished = active;
        }

        private void ApplyEarlyPoisonCheck()
        {
            bool poisoned = false;
            float poisonFactor = FoodUtility.GetFoodPoisonChanceFactor(eater);

            CompFoodPoisonable poisonable = thing.TryGetComp<CompFoodPoisonable>();
            if (poisonable != null && poisonFactor > float.Epsilon && Rand.Chance(poisonable.PoisonPercent * poisonFactor))
            {
                FoodUtility.AddFoodPoisoningHediff(eater, thing, poisonable.Cause);
                poisoned = true;
            }

            CompRottable rottable = thing.TryGetComp<CompRottable>();
            if (rottable != null && rottable.Stage != RotStage.Fresh && poisonFactor > float.Epsilon)
            {
                FoodUtility.AddFoodPoisoningHediff(eater, thing, FoodPoisonCause.Rotten);
                poisoned = true;
            }

            if (eater.RaceProps.Humanlike)
            {
                float poisonChanceOverride;
                float chance = FoodUtility.TryGetFoodPoisoningChanceOverrideFromTraits(eater, thing, out poisonChanceOverride)
                    ? poisonChanceOverride
                    : thing.GetStatValue(StatDefOf.FoodPoisonChanceFixedHuman) * poisonFactor;

                if (Rand.Chance(chance))
                {
                    FoodUtility.AddFoodPoisoningHediff(eater, thing, FoodPoisonCause.DangerousFoodType);
                    poisoned = true;
                }
            }

            ProtectedPoisonChecks.Add(new PoisonKey(eater, thing));

            if (poisoned)
                PartialMealLogger.Debug("poison", "INTERRUPT pawn=" + eater.LabelShort + " food=" + thing.ThingID + " job=" + jobId);

            if (poisoned && actor.CurJob == job)
                actor.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private static float FullNutritionForEater(Pawn eater, Thing food)
        {
            float nutrition = food.GetStatValue(StatDefOf.Nutrition);
            if (eater != null && ModsConfig.BiotechActive && food.def.IsRawHumanFood())
                nutrition *= eater.GetStatValue(StatDefOf.RawNutritionFactor);
            return nutrition;
        }

        public static bool TryFinalize(Job job, Pawn ingester, Thing thing, Action vanillaInitAction)
        {
            if (job == null || thing == null || !ActiveStates.TryGetValue(job.loadID, out PartialMealEatState state) || !state.chewingFinished || state.eater != ingester)
                return false;

            state.FinalizeVanillaIngestion(vanillaInitAction);
            return true;
        }

        private void FinalizeVanillaIngestion(Action vanillaInitAction)
        {
            Need_Food foodNeed = eater.needs?.food;
            float nutritionBeforeFinalize = foodNeed?.CurLevel ?? 0f;
            bool originalIngestTotalCount = job.ingestTotalCount;
            bool originalOvereat = job.overeat;
            float remainingNutritionForOtherItems = initialRemainingNutrition;
            float vanillaUnitNutrition = FullNutritionForEater(eater, thing);

            try
            {
                comp.SetRemainingNutrition(comp.FullNutrition);
                if (foodNeed == null)
                {
                    vanillaInitAction?.Invoke();
                }
                else
                {
                    foodNeed.CurLevel = Mathf.Max(0f, foodNeed.MaxLevel - 0.0001f);
                    job.ingestTotalCount = true;
                    job.overeat = false;
                    vanillaInitAction?.Invoke();

                    foodNeed.CurLevel = nutritionBeforeFinalize;
                    eater.records.AddTo(RecordDefOf.NutritionEaten, -vanillaUnitNutrition * units);
                }

                if (!thing.Destroyed)
                    comp.SetRemainingNutrition(remainingNutritionForOtherItems);
                finalized = true;
                ActiveStates.Remove(jobId);
                PartialMealLogger.Debug("meal", "FINALIZE pawn=" + eater.LabelShort + " food=" + thing.ThingID + " remaining=" + (thing.Destroyed ? 0f : comp.RemainingNutrition));
            }
            finally
            {
                job.ingestTotalCount = originalIngestTotalCount;
                job.overeat = originalOvereat;
                ProtectedPoisonChecks.Remove(new PoisonKey(eater, thing));
            }
        }

        public static void CleanupJob(Job job, JobCondition condition)
        {
            if (job == null || !ActiveStates.TryGetValue(job.loadID, out PartialMealEatState state))
                return;

            if (!state.finalized && state.comp != null)
                state.comp.RemoveIfEmpty();

            ProtectedPoisonChecks.Remove(new PoisonKey(state.eater, state.thing));
            ActiveStates.Remove(job.loadID);
            PartialMealLogger.Debug("meal", "CLEANUP pawn=" + state.eater?.LabelShort + " food=" + state.thing?.ThingID + " condition=" + condition);
        }

        public static bool ShouldSuppressPoison(Pawn pawn, Thing thing)
        {
            return pawn != null && thing != null && ProtectedPoisonChecks.Contains(new PoisonKey(pawn, thing));
        }

        private readonly struct PoisonKey : IEquatable<PoisonKey>
        {
            private readonly int pawnId;
            private readonly int thingId;

            public PoisonKey(Pawn pawn, Thing thing)
            {
                pawnId = pawn?.thingIDNumber ?? -1;
                thingId = thing?.thingIDNumber ?? -1;
            }

            public bool Equals(PoisonKey other)
            {
                return pawnId == other.pawnId && thingId == other.thingId;
            }

            public override bool Equals(object obj)
            {
                return obj is PoisonKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (pawnId * 397) ^ thingId;
            }
        }
    }
}
