using System;
using System.Threading;
using System.Threading.Tasks;

namespace Easy_Save.Model
{
    public class ProcessWatcher : IDisposable
    {
        private static ProcessWatcher _instance;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _watcherTask;
        private readonly BackupRulesManager _rulesManager;
        private bool _isWatching;
        public event EventHandler<string> BusinessSoftwareStarted;
        public event EventHandler<string> BusinessSoftwareStopped;

        private ProcessWatcher()
        {
            _rulesManager = BackupRulesManager.Instance;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public static ProcessWatcher Instance
        {
            get
            {
                _instance ??= new ProcessWatcher();
                return _instance;
            }
        }

        public void StartWatching()
        {
            if (_isWatching) return;

            _isWatching = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _watcherTask = Task.Run(WatchProcessesAsync);
        }

        public void StopWatching()
        {
            if (!_isWatching) return;

            _isWatching = false;
            _cancellationTokenSource.Cancel();
            _watcherTask?.Wait();
        }

        private async Task WatchProcessesAsync()
        {
            string lastRunningSoftware = null;

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    string currentRunningSoftware = _rulesManager.GetRunningBusinessSoftware();

                    if (currentRunningSoftware != lastRunningSoftware)
                    {
                        if (currentRunningSoftware != null)
                        {
                            BusinessSoftwareStarted?.Invoke(this, currentRunningSoftware);
                        }
                        else if (lastRunningSoftware != null)
                        {
                            BusinessSoftwareStopped?.Invoke(this, lastRunningSoftware);
                        }

                        lastRunningSoftware = currentRunningSoftware;
                    }

                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in process watcher: {ex.Message}");
                    await Task.Delay(5000, _cancellationTokenSource.Token);
                }
            }
        }

        public void Dispose()
        {
            StopWatching();
            _cancellationTokenSource?.Dispose();
        }
    }
} 