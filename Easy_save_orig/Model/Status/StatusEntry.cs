using System;
using System.Text.Json.Serialization;

namespace Easy_Save.Model.Status
{
    // Description: Represents a snapshot of the backup job's state for UI or persistence (JSON-serializable).
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

        public StatusEntry(
            string name,
            string sourcePath,
            string destinationPath,
            string state,
            int totalFilesToCopy,
            long totalFilesSize,
            int nbFilesLeftToDo,
            int progression,
            DateTime lastBackupDate)
        // In: name (string), sourcePath (string), destinationPath (string), state (string), totalFilesToCopy (int), totalFilesSize (long), nbFilesLeftToDo (int), progression (int), lastBackupDate (DateTime)
        // Out: StatusEntry instance
        // Description: Initializes a new backup status entry for tracking job metadata and progress.
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
