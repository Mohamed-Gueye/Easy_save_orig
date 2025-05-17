using System;
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
        // Description: Initializes the log observer and sets up a log manager.
        {
            logManager = new LogManager();
        }

        public void Update(Backup backup, long fileSize, double transferTime, int encryptionTime, int fileCount)
        // In: backup (Backup), fileSize (long), transferTime (double), encryptionTime (int), fileCount (int)
        // Out: void
        // Description: Creates and logs a new LogEntry for a backup operation.
        {
            var logEntry = new LogEntry(
                backup.Name,
                backup.SourceDirectory,
                backup.TargetDirectory,
                fileSize,
                transferTime,
                encryptionTime,
                fileCount
            );

            lock (logLock)
            {
                logManager.AddLog(logEntry);
            }
        }
    }
}
