using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Easy_Save.Model
{
    // Description: Singleton class responsible for monitoring business software processes in the background.
    public class ProcessWatcher
    {
        private static ProcessWatcher? _instance;
        private static readonly object _lockObject = new object();
        private readonly BackupRulesManager _rulesManager;
        private readonly Dictionary<string, bool> _processStates = new Dictionary<string, bool>();
        private bool _isWatching = false;
        private CancellationTokenSource? _cancellationTokenSource;

        // Events to notify when a business software starts or stops
        public event EventHandler<string>? BusinessSoftwareStarted;
        public event EventHandler<string>? BusinessSoftwareStopped;

        private ProcessWatcher()
        {
            _rulesManager = BackupRulesManager.Instance;
        }

        public static ProcessWatcher Instance
        // Out: ProcessWatcher
        // Description: Provides thread-safe singleton access to the ProcessWatcher instance.
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new ProcessWatcher();
                    }
                }
                return _instance;
            }
        }

        public void StartWatching()
        // Out: void
        // Description: Starts background monitoring of configured business software using a periodic task loop.
        {
            if (_isWatching)
                return;

            _isWatching = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Initialize state for each software
            foreach (var software in _rulesManager.BusinessSoftwareList)
            {
                if (!string.IsNullOrWhiteSpace(software))
                {
                    bool isRunning = ProcessMonitor.IsProcessRunning(software);
                    _processStates[software] = isRunning;
                }
            }

            // Launch asynchronous monitoring task
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    CheckProcesses();
                    await Task.Delay(1000, token); // Polling interval: 1 second
                }
            }, token);
        }

        public void StopWatching()
        // Out: void
        // Description: Stops the background process monitoring.
        {
            if (!_isWatching)
                return;

            _cancellationTokenSource?.Cancel();
            _isWatching = false;
        }

        private void CheckProcesses()
        // Out: void
        // Description: Checks for changes in the running state of each configured business software and triggers events.
        {
            foreach (var software in _rulesManager.BusinessSoftwareList)
            {
                if (string.IsNullOrWhiteSpace(software))
                    continue;

                bool wasRunning = _processStates.ContainsKey(software) && _processStates[software];
                bool isRunning = ProcessMonitor.IsProcessRunning(software);

                // Detect state change
                if (wasRunning != isRunning)
                {
                    _processStates[software] = isRunning;

                    if (isRunning)
                    {
                        BusinessSoftwareStarted?.Invoke(this, software);
                    }
                    else
                    {
                        BusinessSoftwareStopped?.Invoke(this, software);
                    }
                }
            }
        }
    }
}
