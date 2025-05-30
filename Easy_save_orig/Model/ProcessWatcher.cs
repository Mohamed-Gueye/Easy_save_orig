using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Easy_Save.Model
{
    public class ProcessWatcher
    {
        private static ProcessWatcher? _instance;
        private static readonly object _lockObject = new object();
        private readonly BackupRulesManager _rulesManager;
        private readonly Dictionary<string, bool> _processStates = new Dictionary<string, bool>();
        private bool _isWatching = false;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<string>? BusinessSoftwareStarted;
        public event EventHandler<string>? BusinessSoftwareStopped;

        private ProcessWatcher()
        {
            _rulesManager = BackupRulesManager.Instance;
        }

        public static ProcessWatcher Instance
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
        {
            if (_isWatching)
                return;

            _isWatching = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Initialize process states
            foreach (var software in _rulesManager.BusinessSoftwareList)
            {
                if (!string.IsNullOrWhiteSpace(software))
                {
                    bool isRunning = ProcessMonitor.IsProcessRunning(software);
                    _processStates[software] = isRunning;
                }
            }

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    CheckProcesses();
                    await Task.Delay(1000, token); // Check every second
                }
            }, token);
        }

        public void StopWatching()
        {
            if (!_isWatching)
                return;

            _cancellationTokenSource?.Cancel();
            _isWatching = false;
        }

        private void CheckProcesses()
        {
            foreach (var software in _rulesManager.BusinessSoftwareList)
            {
                if (string.IsNullOrWhiteSpace(software))
                    continue;

                bool wasRunning = _processStates.ContainsKey(software) && _processStates[software];
                bool isRunning = ProcessMonitor.IsProcessRunning(software);

                // If state changed
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
