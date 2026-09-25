using System;
using System.Diagnostics;
#if DEBUG
using RimModUtilities;
#endif
using Verse;

namespace BulkLoad
{
    internal static class BulkLoadDiagnostics
    {
#if DEBUG
        private static readonly ModDiagnostics Logger = ModDiagnostics.Create(
            "pineapplelemonade67.bulkload",
            "bulkload.log",
            minimumLevel: ModLogLevel.Debug,
            writeSessionStart: false);

        static BulkLoadDiagnostics()
        {
        }
#endif

        [Conditional("DEBUG")]
        public static void Info(string channel, string message)
        {
#if DEBUG
            Logger.Info(channel, message);
#endif
        }

        public static void Warning(string channel, string message)
        {
#if DEBUG
            Logger.Warning(channel, message);
#else
            Log.Warning("[BulkLoad] " + message);
#endif
        }

        public static void Error(string channel, string message)
        {
#if DEBUG
            Logger.Error(channel, message);
#else
            Log.Error("[BulkLoad] " + message);
#endif
        }

        [Conditional("DEBUG")]
        public static void Debug(string channel, string message)
        {
#if DEBUG
            Logger.Debug(channel, message);
#endif
        }

        [Conditional("DEBUG")]
        public static void DebugOnce(string channel, string key, string message)
        {
#if DEBUG
            Logger.DebugOnce(channel, key, message);
#endif
        }

        [Conditional("DEBUG")]
        public static void DebugThrottled(string channel, string key, int intervalTicks, string message)
        {
#if DEBUG
            Logger.DebugThrottled(channel, key, intervalTicks, message);
#endif
        }

        [Conditional("DEBUG")]
        public static void Exception(string channel, string key, Exception exception, string message)
        {
#if DEBUG
            Logger.Exception(channel, key, exception, message);
#endif
        }

        [Conditional("DEBUG")]
        public static void RecordModAction(Pawn pawn, string action, string detail = null)
        {
#if DEBUG
            ModDiagnostics.RecordModAction(pawn, "BulkLoad", action, detail);
#endif
        }

        public static string DescribeInventory(Pawn target)
        {
            var inv = target.inventory.innerContainer;
            string summary = "";
            for (int i = 0; i < inv.Count; i++)
            {
                var item = inv[i];
                if (item == null || item.Destroyed)
                    continue;
                summary += item.def?.defName + "x" + item.stackCount + "; ";
            }
            return summary.Length == 0 ? "-" : summary;
        }
    }
}
