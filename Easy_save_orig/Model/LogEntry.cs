using System;
using System.Text.Json.Serialization;

namespace Easy_Save.Model.Log
{
    public class LogEntry
    {
        [JsonPropertyName("Timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("BackupName")]
        public string BackupName { get; set; }

        [JsonPropertyName("SourcePath")]
        public string SourcePath { get; set; }

        [JsonPropertyName("DestinationPath")]
        public string DestinationPath { get; set; }

        [JsonPropertyName("FileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("TransferTime")]
        public long TransferTime { get; set; }

        public LogEntry() { }

        public LogEntry(string backupName, string sourcePath, string destinationPath, long fileSize, double transferTime)
        {
            Timestamp = DateTime.Now;
            BackupName = backupName;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            FileSize = fileSize;
            TransferTime = (long)transferTime;
        }
    }
}
