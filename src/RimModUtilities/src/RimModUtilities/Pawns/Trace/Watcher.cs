using Verse;
#if DEBUG
using System;
using System.Collections.Generic;
using Verse.AI;
using Verse.AI.Group;
#else
using System.Diagnostics;
#endif

namespace RimModUtilities.Pawns.Trace
{
#if DEBUG
    public static class Watcher
    {
        private sealed class WatchState
        {
            public Pawn Pawn;
            public string SessionName;
            public string LastSnapshot;
            public int LastPositionTick;
        }

        private static readonly ModLogger logger = ModLogger.Create(
            "pineapplelemonade67.rimmodutilities",
            "pawn-trace.log");

        private static readonly Dictionary<int, WatchState> watches = new Dictionary<int, WatchState>();

        public static bool TrackPosition { get; set; }
        public static int PositionSampleIntervalTicks { get; set; } = 60;
        public static string TracePath => logger.LogPath;

        static Watcher()
        {
            logger.GameLogEnabled = false;
            logger.MinimumLevel = ModLogLevel.Info;
        }

        public static bool IsWatching(Pawn pawn)
        {
            return pawn != null && watches.ContainsKey(pawn.thingIDNumber);
        }

        public static void Toggle(Pawn pawn)
        {
            if (IsWatching(pawn))
                StopWatching(pawn);
            else
                Watch(pawn);
        }

        public static void Watch(Pawn pawn, string sessionName = null)
        {
            if (pawn == null)
                return;
            if (IsWatching(pawn))
                return;

            string name = string.IsNullOrWhiteSpace(sessionName)
                ? pawn.LabelShort
                : sessionName.Trim();

            WatchState state = new WatchState
            {
                Pawn = pawn,
                SessionName = Clean(name),
                LastSnapshot = Snapshot(pawn),
                LastPositionTick = CurrentTick()
            };

            watches[pawn.thingIDNumber] = state;
            Emit(state, "watch_start", "state=" + state.LastSnapshot);
        }

        public static void StopWatching(Pawn pawn)
        {
            if (pawn == null || !watches.TryGetValue(pawn.thingIDNumber, out WatchState state))
                return;

            Emit(state, "watch_stop", "state=" + Snapshot(pawn));
            watches.Remove(pawn.thingIDNumber);
        }

        public static void RecordModAction(Pawn pawn, string source, string action, string detail = null)
        {
            if (pawn == null || !watches.TryGetValue(pawn.thingIDNumber, out WatchState state))
                return;

            Emit(state, "mod_action", "source=" + Clean(source) + " action=" + Clean(action) + " detail=" + Clean(detail));
        }

        internal static void Tick()
        {
            if (watches.Count == 0)
                return;

            var currentWatches = new List<WatchState>(watches.Values);
            int now = CurrentTick();

            foreach (WatchState state in currentWatches)
            {
                Pawn pawn = state.Pawn;
                if (pawn == null || pawn.Destroyed)
                {
                    Emit(state, "destroyed", "state=destroyed");
                    watches.Remove(pawn?.thingIDNumber ?? 0);
                    continue;
                }

                string snapshot = Snapshot(pawn);
                if (!string.Equals(snapshot, state.LastSnapshot, StringComparison.Ordinal))
                {
                    Emit(state, "state_change", "from=" + state.LastSnapshot + " to=" + snapshot);
                    state.LastSnapshot = snapshot;
                }

                if (TrackPosition && now >= 0 && (state.LastPositionTick < 0 || now - state.LastPositionTick >= PositionSampleIntervalTicks))
                {
                    Emit(state, "position", "position=" + Clean(pawn.Position));
                    state.LastPositionTick = now;
                }
            }
        }

        internal static void JobStarted(Pawn pawn, Job job, ThinkNode jobGiver)
        {
            if (pawn == null || !watches.TryGetValue(pawn.thingIDNumber, out WatchState state))
                return;

            string source = jobGiver == null ? "direct" : jobGiver.GetType().FullName;
            Emit(state, "job_start", "source=" + Clean(source) + " " + DescribeJob(job));
        }

        internal static void JobEnded(Pawn pawn, JobCondition condition)
        {
            if (pawn == null || !watches.TryGetValue(pawn.thingIDNumber, out WatchState state))
                return;

            Emit(state, "job_end", "condition=" + Clean(condition) + " " + DescribeJob(pawn.CurJob));
        }

        internal static void Lifecycle(Pawn pawn, string eventName, string detail = null)
        {
            if (pawn == null || !watches.TryGetValue(pawn.thingIDNumber, out WatchState state))
                return;

            Emit(state, eventName, detail ?? ("state=" + Snapshot(pawn)));
        }

        private static void Emit(WatchState state, string eventName, string detail)
        {
            logger.Info(
                "pawn-trace",
                "event=" + Clean(eventName)
                + " pawnId=" + state.Pawn?.thingIDNumber
                + " pawn=" + Clean(state.Pawn?.LabelShort)
                + " session=" + state.SessionName
                + " tick=" + CurrentTick()
                + " " + Clean(detail));
        }

        private static string Snapshot(Pawn pawn)
        {
            if (pawn == null)
                return "pawn=null";

            Lord lord = pawn.Map?.lordManager?.LordOf(pawn);
            string lordType = lord == null ? "-" : lord.GetType().Name;
            string faction = pawn.Faction?.def?.defName ?? "-";
            string primary = pawn.equipment?.Primary?.def?.defName ?? "-";
            string job = pawn.CurJob?.def?.defName ?? "-";
            string driver = pawn.jobs?.curDriver?.GetType().Name ?? "-";
            string duty = pawn.mindState?.duty?.def?.defName ?? "-";
            string dutyFocus = pawn.mindState?.duty == null
                ? "-"
                    : StableFormatter.Target(pawn.mindState.duty.focus);

            return "spawned=" + pawn.Spawned
                + " dead=" + pawn.Dead
                + " downed=" + pawn.Downed
                + " faction=" + Clean(faction)
                + " lord=" + Clean(lordType)
                + " job=" + Clean(job)
                + " driver=" + Clean(driver)
                + " duty=" + Clean(duty)
                + " dutyFocus=" + Clean(dutyFocus)
                + " primary=" + Clean(primary)
                + " inventory=" + (pawn.inventory?.innerContainer.Count ?? 0);
        }

        private static string DescribeJob(Job job)
        {
            if (job == null)
                return "job=-";

            return "job=" + Clean(job.def?.defName)
                + " targetA=" + Clean(StableFormatter.Target(job, TargetIndex.A))
                + " targetB=" + Clean(StableFormatter.Target(job, TargetIndex.B))
                + " targetC=" + Clean(StableFormatter.Target(job, TargetIndex.C))
                + " count=" + job.count
                + " playerForced=" + job.playerForced;
        }

        private static int CurrentTick()
        {
            return RuntimeHelpers.CurrentTick;
        }

        private static string Clean(object value)
        {
            return (value?.ToString() ?? "-")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "/");
        }
    }
#else
    public static class Watcher
    {
        public static bool TrackPosition { get; set; }
        public static int PositionSampleIntervalTicks { get; set; } = 60;
        public static string TracePath => null;

        public static bool IsWatching(Pawn pawn)
        {
            return false;
        }

        [Conditional("DEBUG")]
        public static void Toggle(Pawn pawn)
        {
        }

        [Conditional("DEBUG")]
        public static void Watch(Pawn pawn, string sessionName = null)
        {
        }

        [Conditional("DEBUG")]
        public static void StopWatching(Pawn pawn)
        {
        }

        [Conditional("DEBUG")]
        public static void RecordModAction(Pawn pawn, string source, string action, string detail = null)
        {
        }
    }
#endif
}
