using System;
using System.Collections.Generic;
using System.IO;
using Easy_Save.Model.Log;

namespace Easy_Save.Model.IO
{
    public class LogManager
    {
        private readonly string _logDir = "logs";
        private List<LogEntry> _logEntries;

        public LogManager()
        {
            string logPath = GetTodayLogPath();
            Directory.CreateDirectory(_logDir);
            _logEntries = WriterManager.Instance.LoadJson<List<LogEntry>>(logPath) ?? new List<LogEntry>();
        }

        public void AddLog(LogEntry entry)
        {
            _logEntries.Add(entry);
            WriterManager.Instance.WriteJson(_logEntries, GetTodayLogPath());
        }

        public List<LogEntry> LoadAllLogs()
        {
            return new List<LogEntry>(_logEntries);
        }

        private string GetTodayLogPath()
        {
            return Path.Combine(_logDir, $"log_{DateTime.Now:yyyy-MM-dd}.json");
        }
    }
}
