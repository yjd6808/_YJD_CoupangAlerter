using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RequestApi.Utils
{
    public delegate void OnDebugLog(LoggerCode code, object[] args);

    public enum LoggerCode
    {
        CrawlTaskWorkerTick,
        CrawlTaskWorkerTimeOver
    }
    public class Logger
    {
        private static Logger s_instance = new Logger();

        private static TextWriter s_logFileWriter;
        private static bool s_closed = true;
        public OnDebugLog OnDebugLog;

        private Logger()
        {
#if DEBUG
            s_logFileWriter = new StreamWriter(new FileStream($"debuglog_{DateTime.Now:yyyy MMMM dd HH-mm-ss}.txt", FileMode.Create, FileAccess.ReadWrite));
            s_closed = false;
#endif
        }

        public static Logger GetInstance()
        {
            return s_instance;
        }

        public void WriteLine(string message)
        {
            lock (this)
            {
                if (s_logFileWriter == null)
                    return;

                if (s_closed)
                    return;

                s_logFileWriter.WriteLine(message);
            }
        }

        public void Close()
        {
            lock (this)
            {
                if (s_logFileWriter == null)
                    return;

                s_logFileWriter.Close();
                s_closed = true;
            }
        }
    }
}
