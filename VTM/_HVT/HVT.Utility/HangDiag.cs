using System;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace HVT.Utility
{
    /// <summary>
    /// UI-freeze watchdog. A DispatcherTimer heartbeats on the UI thread;
    /// a background thread reports to the crash log when the heartbeat stops,
    /// including the last checkpoint so the frozen code path can be identified.
    /// </summary>
    public static class HangDiag
    {
        private static volatile string lastCheckpoint = "startup";
        private static long lastBeatTicks = DateTime.UtcNow.Ticks;
        private static volatile bool started = false;
        private static volatile bool hangReported = false;
        private static string logDirectory = "";

        public static void Checkpoint(string name)
        {
            lastCheckpoint = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + name;
        }

        public static void Start(Dispatcher uiDispatcher, string logDir)
        {
            if (started) return;
            started = true;
            logDirectory = logDir;

            var beatTimer = new DispatcherTimer(DispatcherPriority.Background, uiDispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            beatTimer.Tick += (s, e) =>
            {
                Interlocked.Exchange(ref lastBeatTicks, DateTime.UtcNow.Ticks);
                hangReported = false;
            };
            beatTimer.Start();

            var watcher = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(2000);
                    var silent = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref lastBeatTicks));
                    if (silent.TotalSeconds > 8 && !hangReported)
                    {
                        hangReported = true;
                        WriteHangLog(silent);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "UI-HangWatchdog"
            };
            watcher.Start();
        }

        private static void WriteHangLog(TimeSpan silent)
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
                string logFile = Path.Combine(logDirectory, "CrashLog_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                File.AppendAllText(logFile,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] [HANG] UI thread unresponsive for "
                    + (int)silent.TotalSeconds + "s. Last checkpoint: " + lastCheckpoint
                    + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // never let hang logging itself throw
            }
        }
    }
}
