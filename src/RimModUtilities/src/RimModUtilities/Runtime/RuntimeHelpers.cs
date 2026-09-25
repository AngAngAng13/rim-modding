using System.Diagnostics;
using Verse;

namespace RimModUtilities
{
    public static class RuntimeHelpers
    {
        public static int CurrentTick
        {
            get
            {
                int tick;
                return TryGetCurrentTick(out tick) ? tick : -1;
            }
        }

        public static bool TryGetCurrentTick(out int tick)
        {
            try
            {
                if (Find.TickManager == null)
                {
                    tick = -1;
                    return false;
                }

                tick = Find.TickManager.TicksGame;
                return true;
            }
            catch
            {
                tick = -1;
                return false;
            }
        }

        public static long StopwatchTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        public static double ElapsedMilliseconds(long startTimestamp)
        {
            return ToMilliseconds(Stopwatch.GetTimestamp() - startTimestamp);
        }

        public static double ElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            return ToMilliseconds(endTimestamp - startTimestamp);
        }

        public static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks / (double)Stopwatch.Frequency * 1000.0;
        }
    }
}
