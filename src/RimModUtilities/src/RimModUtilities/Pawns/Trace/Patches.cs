#if DEBUG
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace RimModUtilities.Pawns.Trace
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            RimModUtilities.Pawns.PatchBootstrap.Initialize();
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class StartJobPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

        public static void Postfix(Pawn_JobTracker __instance, Job newJob, ThinkNode jobGiver)
        {
            Watcher.JobStarted(PawnField?.GetValue(__instance) as Pawn, newJob, jobGiver);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class EndJobPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

        public static void Prefix(Pawn_JobTracker __instance, JobCondition condition)
        {
            Watcher.JobEnded(PawnField?.GetValue(__instance) as Pawn, condition);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class KillPatch
    {
        public static void Prefix(Pawn __instance)
        {
            Watcher.Lifecycle(__instance, "killed");
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class DownedPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");

        public static void Prefix(Pawn_HealthTracker __instance)
        {
            Watcher.Lifecycle(PawnField?.GetValue(__instance) as Pawn, "downed");
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public static class SpawnPatch
    {
        public static void Postfix(Thing __instance)
        {
            if (__instance is Pawn pawn)
                Watcher.Lifecycle(pawn, "spawned");
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public static class DespawnPatch
    {
        public static void Prefix(Thing __instance)
        {
            if (__instance is Pawn pawn)
                Watcher.Lifecycle(pawn, "despawned");
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class GizmoPatch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            if (!DebugSettings.ShowDevGizmos)
                yield break;

            bool watching = Watcher.IsWatching(__instance);
            yield return new Command_Action
            {
                defaultLabel = watching ? "DEV: Stop watching pawn" : "DEV: Watch pawn",
                defaultDesc = watching
                    ? "Stops recording this pawn's diagnostic trace."
                    : "Records this pawn's jobs, duties, lifecycle, and state changes.",
                alsoClickIfOtherInGroupClicked = false,
                action = () =>
                {
                    if (watching)
                        Watcher.StopWatching(__instance);
                    else
                        Watcher.Watch(__instance);
                }
            };
        }
    }
}
#endif
