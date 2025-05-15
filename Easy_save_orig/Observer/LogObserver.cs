using System;
using Easy_Save.Model;
using Easy_Save.Model.Log;
using Easy_Save.Model.IO;

namespace Easy_Save.Model.Observer
{
    public class LogObserver
    {
        private readonly LogManager logManager;

        public LogObserver()
        {
            logManager = new LogManager();
        }

        public void Update(Backup backup, long fileSize, double transferTime, int encryptionTime, int fileCount)
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

            logManager.AddLog(logEntry);
        }
    }
}
