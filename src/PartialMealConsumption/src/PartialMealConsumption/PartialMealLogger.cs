using System;
using System.Diagnostics;
#if DEBUG
using RimModUtilities;
#endif

namespace PartialMealConsumption
{
    public static class PartialMealLogger
    {
#if DEBUG
        private static readonly ModDiagnostics logger = ModDiagnostics.Create(
            "pineapplelemonade67.partialmealconsumption",
            "partialmealconsumption.log",
            minimumLevel: ModLogLevel.Info);
#endif

        static PartialMealLogger()
        {
        }

        public static void Initialize()
        {
        }

        [Conditional("DEBUG")]
        public static void Debug(string channel, string message)
        {
#if DEBUG
            logger.Debug(channel, message);
#endif
        }

        [Conditional("DEBUG")]
        public static void DebugThrottled(string channel, string key, int intervalTicks, string message)
        {
#if DEBUG
            logger.DebugThrottled(channel, key, intervalTicks, message);
#endif
        }

        [Conditional("DEBUG")]
        public static void Error(string channel, string message, Exception exception = null)
        {
#if DEBUG
            logger.Error(channel, exception == null ? message : message + Environment.NewLine + exception);
#endif
        }
    }
}
