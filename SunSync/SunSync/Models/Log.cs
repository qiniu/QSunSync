using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    class Log
    {
        private static TraceSource logSource = new TraceSource("QSunSync");
        public static void Init()
        {
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string syncLogPath = System.IO.Path.Combine(myDocPath, "qsunsync", "sync.log");

            logSource.Switch = new SourceSwitch("logSwitch");
            logSource.Switch.Level = SourceLevels.All;
            logSource.Listeners.Remove("Default");

            TextWriterTraceListener prodListener = new TextWriterTraceListener(syncLogPath);
            prodListener.Filter = new EventTypeFilter(SourceLevels.Information);
            prodListener.TraceOutputOptions = TraceOptions.None;
            logSource.Listeners.Add(prodListener);

            /*
            ConsoleTraceListener devListener = new ConsoleTraceListener();
            devListener.Filter = new EventTypeFilter(SourceLevels.Verbose);
            logSource.Listeners.Add(devListener);
            */
        }

        public static void Debug(string message)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            logSource.TraceEvent(TraceEventType.Verbose, 4, string.Format("[{0}] {1}", timestamp, message));
            logSource.Flush();
        }

        public static void Info(string message)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            logSource.TraceEvent(TraceEventType.Information, 3, string.Format("[{0}] {1}", timestamp, message));
            logSource.Flush();
        }

        public static void Warn(string message)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            logSource.TraceEvent(TraceEventType.Warning, 2, string.Format("[{0}] {1}", timestamp, message));
            logSource.Flush();
        }

        public static void Error(string message)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            logSource.TraceEvent(TraceEventType.Error, 1, string.Format("[{0}] {1}", timestamp, message));
            logSource.Flush();
        }

        public static void Fatal(string message)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            logSource.TraceEvent(TraceEventType.Critical, 0, string.Format("[{0}] {1}", timestamp, message));
            logSource.Flush();
        }

        public static void Close()
        {
            if (logSource != null)
            {
                logSource.Close();
            }
        }
    }
}
