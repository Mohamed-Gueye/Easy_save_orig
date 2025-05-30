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
    public class BackupProgressTracker
    {
        private readonly BackupProcess backupProcess;
        private System.Timers.Timer progressTimer;
        private string stateFilePath = "state.json";

        // Dictionary to track current progress for each backup
        private readonly ConcurrentDictionary<string, int> _currentProgress = new ConcurrentDictionary<string, int>();

        public BackupProgressTracker(BackupProcess backupProcess)
        // In: backupProcess (BackupProcess)
        // Out: /
        // Description: Initializes the progress tracker with the backup process and sets the timer.
        {
            this.backupProcess = backupProcess ?? throw new ArgumentNullException(nameof(backupProcess));
            this.progressTimer = new System.Timers.Timer(500);
            this.progressTimer.AutoReset = true;
        }
        public async Task ExecuteBackupWithProgressAsync(string backupName, IProgress<(int Current, int Total)> progress, CancellationToken cancellationToken)
        // In: backupName (string), progress (IProgress), cancellationToken (CancellationToken)
        // Out: Task
        // Description: Executes a backup while reporting progress asynchronously.
        {
            var backup = backupProcess.GetAllBackup().FirstOrDefault(b => b.Name == backupName);
            if (backup == null)
            {
                throw new ArgumentException($"Backup '{backupName}' not found", nameof(backupName));
            }

            // Subscribe to progress tracker events
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

                // Final progress report
                int finalProgress = _currentProgress.GetValueOrDefault(backupName, 100);
                progress.Report((finalProgress, 100));
            }
            finally
            {
                backup.ProgressTrackerInitialized -= OnProgressTrackerInitialized;
                _currentProgress.TryRemove(backupName, out _);
            }
        }
        public async Task<int> ExecuteAllBackupsWithProgressAsync(IProgress<(string BackupName, int Current, int Total)> progress, CancellationToken cancellationToken)
        // In: progress (IProgress), cancellationToken (CancellationToken)
        // Out: Task<int>
        // Description: Executes all backups while tracking and reporting progress WITH priority coordination.
        {
            var backups = backupProcess.GetAllBackup();
            int successCount = 0;

            // Create tasks for ALL backups to run in parallel with priority coordination
            var backupTasks = new List<Task<bool>>();

            foreach (var backup in backups)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string currentBackupName = backup.Name;

                // Subscribe to progress tracker events for this backup
                void OnProgressTrackerInitialized(ByteProgressTracker tracker)
                {
                    Console.WriteLine($"[DEBUG] ProgressTracker initialized for backup: {currentBackupName}");
                    tracker.ProgressChanged += (percentage) =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Console.WriteLine($"[DEBUG] Progress update for {currentBackupName}: {percentage}%");
                            _currentProgress[currentBackupName] = percentage;
                            progress.Report((currentBackupName, percentage, 100));
                        }
                    };
                }

                backup.ProgressTrackerInitialized += OnProgressTrackerInitialized;
                Console.WriteLine($"[DEBUG] Subscribed to ProgressTrackerInitialized for backup: {currentBackupName}");

                // Initial progress report
                _currentProgress[currentBackupName] = 0;
                progress.Report((backup.Name, 0, 100));

                // Create a task for this backup that will coordinate with priority system
                var backupTask = Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Starting backup execution for: {backup.Name}");

                        // Execute backup - this will now properly coordinate with PriorityFileManager
                        await Task.Run(() => backupProcess.ExecuteBackup(backup.Name), cancellationToken);

                        Console.WriteLine($"[DEBUG] Backup execution completed for: {backup.Name}");

                        // Final progress report
                        int finalProgress = _currentProgress.GetValueOrDefault(backup.Name, 100);
                        progress.Report((backup.Name, finalProgress, 100));

                        backup.ProgressTrackerInitialized -= OnProgressTrackerInitialized;
                        _currentProgress.TryRemove(backup.Name, out _);

                        return true; // Success
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
                        return false; // Failure
                    }
                }, cancellationToken);

                backupTasks.Add(backupTask);
            }

            // Wait for all backup tasks to complete
            try
            {
                var results = await Task.WhenAll(backupTasks);
                successCount = results.Count(result => result);
                Console.WriteLine($"[DEBUG] All backups completed. Success count: {successCount}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[DEBUG] Backup execution cancelled");
                throw;
            }

            return successCount;
        }

        private StatusEntry GetBackupStatus(string backupName)
        // In: backupName (string)
        // Out: StatusEntry 
        // Description: Retrieves the backup status from the state file by name.
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
                Console.WriteLine($"Erreur lors de la lecture du fichier d'état: {ex.Message}");
            }

            return null;
        }
    }
}