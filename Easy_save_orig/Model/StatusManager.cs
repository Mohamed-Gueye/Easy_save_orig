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

        public StatusManager()
        {
            _entries = WriterManager.Instance.LoadJson<List<StatusEntry>>(_filePath) ?? new List<StatusEntry>();
        }

        public void UpdateStatus(StatusEntry newEntry)
        {
            _entries.RemoveAll(e => e.Name == newEntry.Name);
            _entries.Add(newEntry);
            WriterManager.Instance.WriteJson(_entries, _filePath);
        }

        public List<StatusEntry> GetAllStatuses()
        {
            return new List<StatusEntry>(_entries);
        }
    }
}
