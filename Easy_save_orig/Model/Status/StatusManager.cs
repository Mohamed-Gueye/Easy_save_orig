using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Easy_Save.Model.Status;

namespace Easy_Save.Model.IO
{
    // Description: Manages reading, writing, and updating backup status entries to a persistent JSON file.
    //              Thread-safe using lock to synchronize access to shared status list.
    public class StatusManager
    {
        private readonly string _filePath = "state.json";
        private List<StatusEntry> _entries;
        private static readonly object statusLock = new();

        public StatusManager()
        // In: none
        // Out: StatusManager instance
        // Description: Initializes the status manager and loads existing status entries from file.
        {
            _entries = WriterManager.Instance.LoadJson<List<StatusEntry>>(_filePath) ?? new List<StatusEntry>();
        }

        public DateTime GetLastBackupDate(string backupName)
        // In: backupName (string)
        // Out: DateTime
        // Description: Returns the last backup time for the specified backup name, or DateTime.MinValue if not found.
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
        // Description: Updates the status entry for the given backup name, writing the updated list to disk.
        {
            lock (statusLock)
            {
                _entries.RemoveAll(e => e.Name == newEntry.Name);
                _entries.Add(newEntry);
                WriterManager.Instance.WriteJson(_entries, _filePath);
            }
        }

        public List<StatusEntry> GetAllStatuses()
        // In: none
        // Out: List<StatusEntry>
        // Description: Returns a new list containing all stored backup status entries.
        {
            lock (statusLock)
            {
                return new List<StatusEntry>(_entries);
            }
        }

        public void RemoveStatus(string name)
        // In: name (string)
        // Out: void
        // Description: Removes the status entry for the given backup name and saves changes to file.
        {
            lock (statusLock)
            {
                _entries.RemoveAll(e => e.Name == name);
                WriterManager.Instance.WriteJson(_entries, _filePath);
            }
        }

        public StatusEntry? GetStatusByName(string name)
        // In: name (string)
        // Out: StatusEntry? (nullable)
        // Description: Retrieves the status entry for the given backup name, or null if not found.
        {
            lock (statusLock)
            {
                return _entries.FirstOrDefault(e => e.Name == name);
            }
        }
    }
}
