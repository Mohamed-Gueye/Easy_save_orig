using System;
using System.Text.Json.Serialization;

namespace Easy_Save.Model.Status
{
    public class StatusEntry
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("SourcePath")]
        public string SourcePath { get; set; }

        [JsonPropertyName("DestinationPath")]
        public string DestinationPath { get; set; }

        [JsonPropertyName("State")]
        public string State { get; set; }

        [JsonPropertyName("TotalFilesToCopy")]
        public int TotalFilesToCopy { get; set; }

        [JsonPropertyName("TotalFilesSize")]
        public long TotalFilesSize { get; set; }

        [JsonPropertyName("NbFilesLeftToDo")]
        public int NbFilesLeftToDo { get; set; }

        [JsonPropertyName("Progression")]
        public int Progression { get; set; }

        [JsonPropertyName("LastBackupDate")]
        public DateTime LastBackupDate { get; set; }

        public StatusEntry(string name, string sourcePath, string destinationPath, string state, int totalFilesToCopy, long totalFilesSize, int nbFilesLeftToDo, int progression, DateTime lastBackupDate)
        // In: All thz status data (string, int, long, DateTime)
        // Out: /
        // Description: Initializes a status entry representing the state of a backup job.
        {
            Name = name;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            State = state;
            TotalFilesToCopy = totalFilesToCopy;
            TotalFilesSize = totalFilesSize;
            NbFilesLeftToDo = nbFilesLeftToDo;
            Progression = progression;
            LastBackupDate = lastBackupDate;
        }
    }
}
