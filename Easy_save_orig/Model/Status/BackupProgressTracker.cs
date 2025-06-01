using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Linq;
using Easy_Save.Controller;
using Easy_Save.Model.Status;
using System.Timers;
using System.Collections.Concurrent;

namespace Easy_Save.Model
{
    /// <summary>
    /// Tracks progress for backup operations and handles asynchronous execution and reporting.
    /// Uses events and progress reporting patterns in combination with CancellationTokens.
    /// </summary>
    public class BackupProgressTracker
    {
        private readonly BackupProcess backupProcess;
        private System.Timers.Timer progressTimer;
        private string stateFilePath = "state.json";

        // Stores progress percentage per backup using a thread-safe dictionary
        private readonly ConcurrentDictionary<string, int> _currentProgress = new ConcurrentDictionary<string, int>();

        // In: backupProcess (BackupProcess)
        // Out: /
        // Description: Initializes the progress tracker with the backup process and sets the progress timer.
        public BackupProgressTracker(BackupProcess backupProcess)
        {
            this.backupProcess = backupProcess ?? throw new ArgumentNullException(nameof(backupProcess));
            this.progressTimer = new System.Timers.Timer(500);
            this.progressTimer.AutoReset = true;
        }

        // In: backupName (string), progress (IProgress), cancellationToken (CancellationToken)
        // Out: Task
        // Description: Executes a single backup while reporting progress asynchronously through IProgress.
        public async Task ExecuteBackupWithProgressAsync(string backupName, IProgress<(int Current, int Total)> progress, CancellationToken cancellationToken)
        {
            var backup = backupProcess.GetAllBackup().FirstOrDefault(b => b.Name == backupName);
            if (backup == null)
                throw new ArgumentException($"Backup '{backupName}' not found", nameof(backupName));

            // Event subscription: triggers when ByteProgressTracker is initialized for this backup
            void OnProgressTrackerInitialized(ByteProgressTracker tracker)
            {
                tracker.ProgressChanged += (percentage) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _currentProgress[backupName] = percentage;
                        progress.Report((percentage, 100));
                    }
                };
            }

            backup.ProgressTrackerInitialized += OnProgressTrackerInitialized;

            try
            {
                _currentProgress[backupName] = 0;
                progress.Report((0, 100));

                await Task.Run(() => backupProcess.ExecuteBackup(backupName), cancellationToken);

                int finalProgress = _currentProgress.GetValueOrDefault(backupName, 100);
                progress.Report((finalProgress, 100));
            }
            finally
            {
                backup.ProgressTrackerInitialized -= OnProgressTrackerInitialized;
                _currentProgress.TryRemove(backupName, out _);
            }
        }

        // In: progress (IProgress), cancellationToken (CancellationToken)
        // Out: Task<int>
        // Description: Executes all backups asynchronously with progress tracking and cancellation support. Returns number of successful backups.
        public async Task<int> ExecuteAllBackupsWithProgressAsync(IProgress<(string BackupName, int Current, int Total)> progress, CancellationToken cancellationToken)
        {
            var backups = backupProcess.GetAllBackup();
            int successCount = 0;
            var backupTasks = new List<Task<bool>>();

            foreach (var backup in backups)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                string currentBackupName = backup.Name;

                // Event handler for byte-level progress changes
                void OnProgressTrackerInitialized(ByteProgressTracker tracker)
                {
                    tracker.ProgressChanged += (percentage) =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            _currentProgress[currentBackupName] = percentage;
                            progress.Report((currentBackupName, percentage, 100));
                        }
                    };
                }

                backup.ProgressTrackerInitialized += OnProgressTrackerInitialized;

                _currentProgress[currentBackupName] = 0;
                progress.Report((backup.Name, 0, 100));

                var backupTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Run(() => backupProcess.ExecuteBackup(backup.Name), cancellationToken);

                        int finalProgress = _currentProgress.GetValueOrDefault(backup.Name, 100);
                        progress.Report((backup.Name, finalProgress, 100));

                        backup.ProgressTrackerInitialized -= OnProgressTrackerInitialized;
                        _currentProgress.TryRemove(backup.Name, out _);

                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        backup.ProgressTrackerInitialized -= OnProgressTrackerInitialized;
                        _currentProgress.TryRemove(backup.Name, out _);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Backup {backup.Name} failed: {ex.Message}");
                        backup.ProgressTrackerInitialized -= OnProgressTrackerInitialized;
                        _currentProgress.TryRemove(backup.Name, out _);
                        return false;
                    }
                }, cancellationToken);

                backupTasks.Add(backupTask);
            }

            try
            {
                var results = await Task.WhenAll(backupTasks);
                successCount = results.Count(result => result);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[DEBUG] Backup execution cancelled");
                throw;
            }

            return successCount;
        }

        // In: backupName (string)
        // Out: StatusEntry
        // Description: Loads and returns the status entry of a backup from the state file by name.
        private StatusEntry GetBackupStatus(string backupName)
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    string json = File.ReadAllText(stateFilePath);
                    List<StatusEntry> statusEntries = JsonSerializer.Deserialize<List<StatusEntry>>(json);

                    if (statusEntries != null)
                    {
                        return statusEntries.FirstOrDefault(e => e.Name == backupName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading state file: {ex.Message}");
            }

            return null;
        }
    }
}
