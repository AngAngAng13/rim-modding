using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace RaidFlow
{
    public static class RaidFlowProfiler
    {
        private const int MaxSamples = 2048;

        private static readonly object sync = new object();
        private static readonly List<double> sharedTimes = new List<double>();
        private static readonly List<double> normalTimes = new List<double>();
        private static readonly Dictionary<PathRequest, long> normalStarts = new Dictionary<PathRequest, long>();

        public static void RecordShared(double milliseconds)
        {
            lock (sync)
                AddCapped(sharedTimes, milliseconds);
        }

        public static void RecordNormalStart(PathRequest request)
        {
            lock (sync)
            {
                if (normalStarts.Count > 4096)
                    normalStarts.Clear();
                normalStarts[request] = Stopwatch.GetTimestamp();
            }
        }

        public static void FinishNormal(PathRequest request)
        {
            lock (sync)
            {
                if (!normalStarts.TryGetValue(request, out long start))
                    return;
                normalStarts.Remove(request);
                AddCapped(normalTimes, ElapsedMs(start));
            }
        }

        public static void Reset()
        {
            lock (sync)
            {
                sharedTimes.Clear();
                normalTimes.Clear();
                normalStarts.Clear();
            }
        }

        public static bool TryGetStats(bool shared, out int count, out double typical, out double slowest)
        {
            double[] snapshot;
            lock (sync)
            {
                List<double> source = shared ? sharedTimes : normalTimes;
                snapshot = source.ToArray();
            }
            count = snapshot.Length;
            typical = 0.0;
            slowest = 0.0;
            if (count == 0)
                return false;
            System.Array.Sort(snapshot);
            typical = snapshot[count / 2];
            slowest = snapshot[(int)(count * 0.99)];
            return true;
        }

        public static double ElapsedMs(long start)
        {
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private static void AddCapped(List<double> samples, double milliseconds)
        {
            if (samples.Count >= MaxSamples)
                samples.RemoveAt(0);
            samples.Add(milliseconds);
        }
    }
}
