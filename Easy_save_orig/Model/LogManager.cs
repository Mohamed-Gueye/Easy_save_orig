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
        // Description: Initializes the log manager and loads today's log entries.
        {
            string logPath = GetTodayLogPath();
            Directory.CreateDirectory(_logDir);
            _logEntries = WriterManager.Instance.LoadJson<List<LogEntry>>(logPath) ?? new List<LogEntry>();
        }

        public void AddLog(LogEntry entry)
        // In: entry (LogEntry)
        // Out: void
        // Description: Adds a new log entry and saves it to today's log file.
        {
            _logEntries.Add(entry);
            WriterManager.Instance.WriteJson(_logEntries, GetTodayLogPath());
        }

        public List<LogEntry> LoadAllLogs()
        // Out: List<LogEntry> 
        // Description: Returns all current log entries.
        {
            return new List<LogEntry>(_logEntries);
        }

        private string GetTodayLogPath()
        // Out: string
        // Description: Returns the file path for today's log file.
        {
            return Path.Combine(_logDir, $"log_{DateTime.Now:yyyy-MM-dd}.json");
        }
    }
}