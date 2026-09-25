using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Verse;

namespace RimModUtilities
{
    public sealed class ModLogger
    {
        private sealed class ThrottleState
        {
            public int LastTick;
            public int SuppressedCount;
        }

        private readonly object sync = new object();
        private readonly HashSet<string> disabledChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> onceKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ThrottleState> throttleStates = new Dictionary<string, ThrottleState>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> changedValues = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly string logPath;
        private const int BufferedLineFlushCount = 32;
        private const int BufferedFlushIntervalTicks = 120;
        private StreamWriter fileWriter;
        private int bufferedLineCount;
        private int lastFileFlushTick = -1;

        public string ModId { get; }
        public ModLogLevel MinimumLevel { get; set; } = ModLogLevel.Info;
        public bool GameLogEnabled { get; set; } = true;
        public bool FileLogEnabled { get; set; } = true;
        public string LogPath => logPath;

        private ModLogger(string modId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(modId))
                throw new ArgumentException("A mod id is required.", nameof(modId));

            ModId = modId.Trim();

            string safeFileName = SanitizeFileName(
                string.IsNullOrWhiteSpace(fileName) ? ModId + ".log" : Path.GetFileName(fileName));
            logPath = Path.Combine(Path.GetTempPath(), "RimWorldModLogs", safeFileName);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.DomainUnload += OnProcessExit;
        }

        public static ModLogger Create(string modId, string fileName = null)
        {
            return new ModLogger(modId, fileName);
        }

        public void SetChannelEnabled(string channel, bool enabled)
        {
            string normalized = NormalizeChannel(channel);
            lock (sync)
            {
                if (enabled)
                    disabledChannels.Remove(normalized);
                else
                    disabledChannels.Add(normalized);
            }
        }

        public bool IsChannelEnabled(string channel)
        {
            string normalized = NormalizeChannel(channel);
            lock (sync)
                return !disabledChannels.Contains(normalized);
        }

        [Conditional("DEBUG")]
        public void Debug(string channel, string message)
        {
            Write(ModLogLevel.Debug, channel, message);
        }

        public void Info(string channel, string message)
        {
            Write(ModLogLevel.Info, channel, message);
        }

        public void Warning(string channel, string message)
        {
            Write(ModLogLevel.Warning, channel, message);
        }

        public void Error(string channel, string message)
        {
            Write(ModLogLevel.Error, channel, message);
        }

        [Conditional("DEBUG")]
        public void DebugOnce(string channel, string key, string message)
        {
            Once(ModLogLevel.Debug, channel, key, message);
        }

        [Conditional("DEBUG")]
        public void DebugThrottled(string channel, string key, int intervalTicks, string message)
        {
            Throttled(ModLogLevel.Debug, channel, key, intervalTicks, message);
        }

        [Conditional("DEBUG")]
        public void DebugChanged(string channel, string key, string value, string message)
        {
            Changed(ModLogLevel.Debug, channel, key, value, message);
        }

        [Conditional("DEBUG")]
        public void DebugException(string channel, string key, Exception exception, string message = null)
        {
            Exception(channel, key, exception, message);
        }

        public void Once(ModLogLevel level, string channel, string key, string message)
        {
            if (!ShouldWrite(level, channel))
                return;

            string dedupeKey = BuildKey(channel, key, message);
            lock (sync)
            {
                if (!onceKeys.Add(dedupeKey))
                    return;
            }

            Emit(level, channel, message);
        }

        public void Throttled(ModLogLevel level, string channel, string key, int intervalTicks, string message)
        {
            ThrottledCore(level, channel, key, intervalTicks, message, null);
        }

        public void Throttled(ModLogLevel level, string channel, string key, int intervalTicks, Func<string> messageFactory)
        {
            ThrottledCore(level, channel, key, intervalTicks, null, messageFactory);
        }

        private void ThrottledCore(
            ModLogLevel level,
            string channel,
            string key,
            int intervalTicks,
            string message,
            Func<string> messageFactory)
        {
            if (!ShouldWrite(level, channel))
                return;

            string resolvedMessage = null;
            if (string.IsNullOrWhiteSpace(key))
                resolvedMessage = ResolveMessage(message, messageFactory);

            if (intervalTicks <= 0)
            {
                Emit(level, channel, resolvedMessage ?? ResolveMessage(message, messageFactory));
                return;
            }

            int now = CurrentTick();
            if (now < 0)
            {
                Emit(level, channel, resolvedMessage ?? ResolveMessage(message, messageFactory));
                return;
            }

            string throttleKey = BuildKey(channel, key, resolvedMessage);
            int suppressedCount = 0;

            lock (sync)
            {
                if (!throttleStates.TryGetValue(throttleKey, out ThrottleState state))
                {
                    state = new ThrottleState { LastTick = now };
                    throttleStates[throttleKey] = state;
                }
                else if (now >= state.LastTick && now - state.LastTick < intervalTicks)
                {
                    state.SuppressedCount++;
                    return;
                }
                else
                {
                    suppressedCount = state.SuppressedCount;
                    state.LastTick = now;
                    state.SuppressedCount = 0;
                }
            }

            resolvedMessage = resolvedMessage ?? ResolveMessage(message, messageFactory);
            if (suppressedCount > 0)
                resolvedMessage += " (suppressed " + suppressedCount + " repeats)";

            Emit(level, channel, resolvedMessage);
        }

        public void Changed(ModLogLevel level, string channel, string key, string value, string message)
        {
            if (!ShouldWrite(level, channel))
                return;

            string changeKey = BuildKey(channel, key, null);
            lock (sync)
            {
                if (changedValues.TryGetValue(changeKey, out string previous) &&
                    string.Equals(previous, value, StringComparison.Ordinal))
                {
                    return;
                }

                changedValues[changeKey] = value;
            }

            Emit(level, channel, message);
        }

        public void Exception(string channel, string key, Exception exception, string message = null)
        {
            string prefix = string.IsNullOrWhiteSpace(message) ? "Unhandled exception" : message;
            string detail = exception == null ? prefix : prefix + Environment.NewLine + exception;

            if (string.IsNullOrWhiteSpace(key))
                Error(channel, detail);
            else
                Once(ModLogLevel.Error, channel, key, detail);
        }

        public void ResetDeduplication()
        {
            lock (sync)
            {
                onceKeys.Clear();
                throttleStates.Clear();
                changedValues.Clear();
            }
        }

        public void Flush()
        {
            try
            {
                lock (sync)
                    FlushFile();
            }
            catch
            {
                ResetFileWriter();
            }
        }

        private void Write(ModLogLevel level, string channel, string message)
        {
            if (!ShouldWrite(level, channel))
                return;

            Emit(level, channel, message);
        }

        private bool ShouldWrite(ModLogLevel level, string channel)
        {
            if (level < MinimumLevel)
                return false;

            return IsChannelEnabled(channel);
        }

        private void Emit(ModLogLevel level, string channel, string message)
        {
            string normalizedChannel = NormalizeChannel(channel);
            string line = "[" + ModId + "][" + normalizedChannel + "][" + level.ToString().ToUpperInvariant() + "][t=" + CurrentTick() + "] " + (message ?? string.Empty);

            if (GameLogEnabled)
                WriteGameLog(level, line);

            if (FileLogEnabled)
                WriteFile(line);
        }

        private static void WriteGameLog(ModLogLevel level, string line)
        {
            switch (level)
            {
                case ModLogLevel.Error:
                    Log.Error(line);
                    break;
                case ModLogLevel.Warning:
                    Log.Warning(line);
                    break;
                default:
                    Log.Message(line);
                    break;
            }
        }

        private void WriteFile(string line)
        {
            try
            {
                lock (sync)
                {
                    EnsureFileWriter();
                    if (fileWriter == null)
                        return;

                    fileWriter.WriteLine(line);
                    bufferedLineCount++;
                    int now = CurrentTick();
                    if (bufferedLineCount >= BufferedLineFlushCount
                        || (now >= 0
                            && (lastFileFlushTick < 0 || now - lastFileFlushTick >= BufferedFlushIntervalTicks)))
                    {
                        FlushFile();
                    }
                }
            }
            catch
            {
                ResetFileWriter();
            }
        }

        private void EnsureFileWriter()
        {
            if (fileWriter != null)
                return;

            string directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            FileStream stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            fileWriter = new StreamWriter(stream);
            bufferedLineCount = 0;
            lastFileFlushTick = CurrentTick();
        }

        private void FlushFile()
        {
            if (fileWriter == null)
                return;

            fileWriter.Flush();
            bufferedLineCount = 0;
            lastFileFlushTick = CurrentTick();
        }

        private void ResetFileWriter()
        {
            lock (sync)
            {
                try
                {
                    fileWriter?.Dispose();
                }
                catch
                {
                }

                fileWriter = null;
                bufferedLineCount = 0;
                lastFileFlushTick = -1;
            }
        }

        private void OnProcessExit(object sender, EventArgs args)
        {
            Flush();
        }

        private static string ResolveMessage(string message, Func<string> messageFactory)
        {
            return messageFactory == null ? message ?? string.Empty : messageFactory() ?? string.Empty;
        }

        private static int CurrentTick()
        {
            return RuntimeHelpers.CurrentTick;
        }

        private string BuildKey(string channel, string key, string message)
        {
            return ModId + "|" + NormalizeChannel(channel) + "|" + (string.IsNullOrWhiteSpace(key) ? message ?? string.Empty : key);
        }

        private static string NormalizeChannel(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                return "general";

            return channel.Trim().Replace("]", ")").Replace("\r", " ").Replace("\n", " ");
        }

        private static string SanitizeFileName(string fileName)
        {
            string value = string.IsNullOrWhiteSpace(fileName) ? "mod.log" : fileName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}
