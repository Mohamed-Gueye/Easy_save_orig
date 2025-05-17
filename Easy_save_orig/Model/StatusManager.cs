using System;
using System.Collections.Generic;
using System.IO;
using Easy_Save.Model.Status;

namespace Easy_Save.Model.IO
{
    public class StatusManager
    {
        private readonly string _filePath = "state.json";
        private List<StatusEntry> _entries;
        private static readonly object statusLock = new();

        public StatusManager()
        // Description: Initializes the status manager and loads existing status entries from file.
        {
            _entries = WriterManager.Instance.LoadJson<List<StatusEntry>>(_filePath) ?? new List<StatusEntry>();
        }

        public DateTime GetLastBackupDate(string backupName)
        // In: backupName (string)
        // Out: DateTime
        // Description: Returns the last backup time for the specified backup name.
        {
            lock (statusLock)
            {
                var entry = _entries.Find(e => e.Name == backupName);
                return entry?.LastBackupDate ?? DateTime.MinValue;
            }
        }

        public void UpdateStatus(StatusEntry newEntry)
        // In: newEntry (StatusEntry)
        // Out: void
        // Description: Updates the status entry for the given backup name.
        {
            lock (statusLock)
            {
                _entries.RemoveAll(e => e.Name == newEntry.Name);
                _entries.Add(newEntry);
                WriterManager.Instance.WriteJson(_entries, _filePath);
            }
        }

        public List<StatusEntry> GetAllStatuses()
        // Out: List<StatusEntry>
        // Description: Returns all stored backup status entries.
        {
            lock (statusLock)
            {
                return new List<StatusEntry>(_entries);
            }
        }

        public void RemoveStatus(string name)
        // In: name (string)
        // Out: void
        // Description: Removes the status entry for the given backup name.
        {
            lock (statusLock)
            {
                _entries.RemoveAll(e => e.Name == name);
                WriterManager.Instance.WriteJson(_entries, _filePath);
            }
        }
    }
}