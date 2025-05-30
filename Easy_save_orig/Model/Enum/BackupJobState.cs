using System;

namespace Easy_Save.Model.Enum
{    /// <summary>
     /// Represents the current state of a backup job
     /// </summary>
    public enum BackupJobState
    {
        READY,
        RUNNING,
        PAUSED,
        PAUSED_FOR_PRIORITY,
        STOPPED,
        COMPLETED,
        ERROR
    }
}
