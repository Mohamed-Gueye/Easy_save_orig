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

        [JsonPropertyName("EncryptionTime")]
        public long EncryptionTime { get; set; }

        [JsonPropertyName("FileCount")]
        public int FileCount { get; set; }

        public LogEntry() { }

        public LogEntry(string backupName, string sourcePath, string destinationPath, long fileSize, double transferTime, int encryptionTime, int fileCount)
        // In: backupName (string), sourcePath (string), destinationPath (string), fileSize (long), transferTime (double), encryptionTime (int), fileCount (int)
        // Out: /
        // Description: Constructs a log entry with the provided values and current time.
        // Description: Constructs a log entry with the provided values and current time.
        {
            Timestamp = DateTime.Now;
            BackupName = backupName;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            FileSize = fileSize;
            TransferTime = (long)transferTime;
            EncryptionTime = encryptionTime;
            FileCount = fileCount;
        }
    }
}
