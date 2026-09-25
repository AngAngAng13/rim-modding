using System;
using System.Diagnostics;
using Verse;
using RimModUtilities.Pawns.Trace;

namespace RimModUtilities
{
    public sealed class ModDiagnostics
    {
        private readonly ModLogger logger;
        private readonly string defaultChannel;
        private readonly Func<string, string> channelSelector;
        private bool sessionStarted;

        private ModDiagnostics(
            string modId,
            string fileName,
            string defaultChannel,
            Func<string, string> channelSelector,
            ModLogLevel minimumLevel,
            bool gameLogEnabled,
            bool fileLogEnabled,
            bool writeSessionStart)
        {
            logger = ModLogger.Create(modId, fileName);
            this.defaultChannel = string.IsNullOrWhiteSpace(defaultChannel) ? "general" : defaultChannel;
            this.channelSelector = channelSelector;
            logger.MinimumLevel = minimumLevel;
            logger.GameLogEnabled = gameLogEnabled;
            logger.FileLogEnabled = fileLogEnabled;

            if (writeSessionStart)
                Initialize();
        }

        public ModLogger Logger => logger;
        public string ModId => logger.ModId;
        public string LogPath => logger.LogPath;

        public static ModDiagnostics Create(
            string modId,
            string fileName = null,
            string defaultChannel = "general",
            Func<string, string> channelSelector = null,
            ModLogLevel minimumLevel = ModLogLevel.Info,
            bool gameLogEnabled = false,
            bool fileLogEnabled = true,
            bool writeSessionStart = true)
        {
            return new ModDiagnostics(
                modId,
                fileName,
                defaultChannel,
                channelSelector,
                minimumLevel,
                gameLogEnabled,
                fileLogEnabled,
                writeSessionStart);
        }

        public void Initialize(string sessionMessage = "SESSION_START")
        {
            if (sessionStarted || string.IsNullOrWhiteSpace(sessionMessage))
                return;

            sessionStarted = true;
            logger.Info("lifecycle", sessionMessage);
        }

        public void Info(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Info(ChannelFor(message), message);
        }

        public void Info(string channel, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Info(channel, message);
        }

        public void Warning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Warning(ChannelFor(message), message);
        }

        public void Warning(string channel, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Warning(channel, message);
        }

        public void Error(string message, Exception exception = null)
        {
            Error(ChannelFor(message), null, message, exception);
        }

        public void Error(string channel, string message, Exception exception = null)
        {
            Error(channel, null, message, exception);
        }

        public void Error(string channel, string key, string message, Exception exception = null)
        {
            if (string.IsNullOrWhiteSpace(message) && exception == null)
                return;

            if (exception == null)
            {
                logger.Error(channel, message);
                return;
            }

            logger.Exception(channel, key, exception, message);
        }

        [Conditional("DEBUG")]
        public void Debug(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Debug(ChannelFor(message), message);
        }

        [Conditional("DEBUG")]
        public void Debug(string channel, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Debug(channel, message);
        }

        [Conditional("DEBUG")]
        public void DebugOnce(string key, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.DebugOnce(ChannelFor(message), key, message);
        }

        [Conditional("DEBUG")]
        public void DebugOnce(string channel, string key, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.DebugOnce(channel, key, message);
        }

        [Conditional("DEBUG")]
        public void DebugThrottled(string key, int intervalTicks, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.DebugThrottled(ChannelFor(message), key, intervalTicks, message);
        }

        [Conditional("DEBUG")]
        public void DebugThrottled(string key, int intervalTicks, Func<string> messageFactory)
        {
            logger.Throttled(ModLogLevel.Debug, defaultChannel, key, intervalTicks, messageFactory);
        }

        [Conditional("DEBUG")]
        public void DebugThrottled(string channel, string key, int intervalTicks, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.DebugThrottled(channel, key, intervalTicks, message);
        }

        [Conditional("DEBUG")]
        public void DebugChanged(string key, string value, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.DebugChanged(ChannelFor(message), key, value, message);
        }

        [Conditional("DEBUG")]
        public void DebugChanged(string channel, string key, string value, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.DebugChanged(channel, key, value, message);
        }

        [Conditional("DEBUG")]
        public void DebugException(string key, Exception exception, string message = null)
        {
            logger.DebugException(ChannelFor(message), key, exception, message);
        }

        [Conditional("DEBUG")]
        public void DebugException(string channel, string key, Exception exception, string message = null)
        {
            logger.DebugException(channel, key, exception, message);
        }

        public void Once(ModLogLevel level, string key, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Once(level, ChannelFor(message), key, message);
        }

        public void Once(ModLogLevel level, string channel, string key, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Once(level, channel, key, message);
        }

        public void Throttled(ModLogLevel level, string key, int intervalTicks, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Throttled(level, ChannelFor(message), key, intervalTicks, message);
        }

        public void Throttled(ModLogLevel level, string key, int intervalTicks, Func<string> messageFactory)
        {
            logger.Throttled(level, defaultChannel, key, intervalTicks, messageFactory);
        }

        public void Throttled(ModLogLevel level, string channel, string key, int intervalTicks, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logger.Throttled(level, channel, key, intervalTicks, message);
        }

        public void Throttled(ModLogLevel level, string channel, string key, int intervalTicks, Func<string> messageFactory)
        {
            logger.Throttled(level, channel, key, intervalTicks, messageFactory);
        }

        public void Exception(string key, Exception exception, string message = null)
        {
            logger.Exception(ChannelFor(message), key, exception, message);
        }

        public void Exception(string channel, string key, Exception exception, string message = null)
        {
            logger.Exception(channel, key, exception, message);
        }

        public void SetChannelEnabled(string channel, bool enabled)
        {
            logger.SetChannelEnabled(channel, enabled);
        }

        public bool IsChannelEnabled(string channel)
        {
            return logger.IsChannelEnabled(channel);
        }

        public void ResetDeduplication()
        {
            logger.ResetDeduplication();
        }

        public void Flush()
        {
            logger.Flush();
        }

        [Conditional("DEBUG")]
        public static void RecordModAction(Pawn pawn, string source, string action, string detail = null)
        {
            Watcher.RecordModAction(pawn, source, action, detail);
        }

        private string ChannelFor(string message)
        {
            if (channelSelector == null)
                return defaultChannel;

            try
            {
                string selected = channelSelector(message);
                return string.IsNullOrWhiteSpace(selected) ? defaultChannel : selected;
            }
            catch
            {
                return defaultChannel;
            }
        }
    }
}
