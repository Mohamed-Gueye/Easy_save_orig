using System;
using System.IO;
using Easy_Save.Model;
using Easy_Save.Model.Log;
using Easy_Save.Model.IO;

namespace Easy_Save.Model.Observer
{
    public class LogObserver
    {
        private readonly LogManager logManager;
        private static readonly object logLock = new();

        public LogObserver()
        {
            logManager = new LogManager();
        }

        public void Update(Backup backup, long fileSize, double transferTime)
        {
            var logEntry = new LogEntry(
                backup.Name,
                backup.SourceDirectory,
                backup.TargetDirectory,
                fileSize,
                transferTime
            );

            lock (logLock)
            {
                logManager.AddLog(logEntry);
            }
        }
    }
}
