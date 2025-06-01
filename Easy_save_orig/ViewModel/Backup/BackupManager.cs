using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Easy_Save.Interfaces;
using Easy_Save.Model;
using Easy_Save.Model.Observer;
using Easy_Save.Model.IO;
using Easy_Save.Strategies;
using Easy_Save.Model.Enum;

namespace Easy_Save.Model
{
    public class BackupManager
    {
        private List<Backup> backups = new();
        private readonly StatusManager statusManager;
        private readonly LogObserver logObserver;
        private readonly ProcessWatcher processWatcher;
        private bool currentJobAllowedToComplete = true;
        private readonly ConcurrentDictionary<string, bool> runningJobs = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, bool> pausedJobs = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> backupCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

        public event EventHandler<BackupPausedEventArgs>? BackupPaused;
        public event EventHandler<BackupResumedEventArgs>? BackupResumed;
        public event EventHandler<BackupStateChangedEventArgs>? BackupStateChanged;

        public BackupManager()
        // Out: /
        // Description: Initializes the backup manager, sets up process watchers and cleans orphan statuses.
        {
            statusManager = new StatusManager();
            logObserver = new LogObserver();
            processWatcher = ProcessWatcher.Instance;

            processWatcher.BusinessSoftwareStarted += OnBusinessSoftwareStarted;
            processWatcher.BusinessSoftwareStopped += OnBusinessSoftwareStopped;

            processWatcher.StartWatching();

            CleanOrphanStatuses();
        }

        private void OnBusinessSoftwareStarted(object sender, string softwareName)
        {
            foreach (var job in runningJobs.Where(j => j.Value))
            {
                PauseBackup(job.Key);
                BackupPaused?.Invoke(this, new BackupPausedEventArgs(job.Key, softwareName));
            }
        }

        private void OnBusinessSoftwareStopped(object sender, string softwareName)
        {
            foreach (var job in pausedJobs.Where(j => j.Value))
            {
                ResumeBackup(job.Key);
                BackupResumed?.Invoke(this, new BackupResumedEventArgs(job.Key, softwareName));
            }
        }

        public void PauseBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Pauses the backup job with the given name and updates its status.
        {
            if (runningJobs.TryGetValue(name, out bool isRunning) && isRunning)
            {
                var backup = backups.FirstOrDefault(b => b.Name == name);
                if (backup != null)
                {
                    backup.Pause();
                    pausedJobs[name] = true;

                    var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                    if (status != null)
                    {
                        status.State = "PAUSED";
                        statusManager.UpdateStatus(status);
                    }

                    BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
                }
            }
        }
        public void ResumeBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Resumes a paused backup job if business software is no longer running.
        {
            if (pausedJobs.TryGetValue(name, out bool isPaused) && isPaused)
            {
                var backup = backups.FirstOrDefault(b => b.Name == name);
                if (backup != null && !BackupRulesManager.Instance.IsAnyBusinessSoftwareRunning())
                {
                    backup.Play();
                    pausedJobs[name] = false;

                    var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                    if (status != null)
                    {
                        status.State = "ACTIVE";
                        statusManager.UpdateStatus(status);
                    }

                    BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
                    BackupResumed?.Invoke(this, new BackupResumedEventArgs(name, "Manual resume"));
                }
            }
        }

        public void StopBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Stops the backup job and cancels its associated token. Updates status to STOPPED.
        {
            var backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup != null)
            {
                backup.Stop();

                if (backupCancellationTokens.TryGetValue(name, out var tokenSource))
                {
                    tokenSource.Cancel();
                }

                runningJobs[name] = false;
                pausedJobs[name] = false;

                var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                if (status != null)
                {
                    status.State = "STOPPED";
                    statusManager.UpdateStatus(status);
                }

                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));

                Console.WriteLine($"Backup '{name}' has been stopped.");
            }
        }

        public void AddBackup(Backup backup)
        // In: backup (Backup)
        // Out: void
        // Description: Adds a new backup to the internal backup list.
        {
            if (backup == null)
                throw new ArgumentNullException(nameof(backup));

            backups.Add(backup);
        }

        public void RemoveBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Removes a backup by name and deletes its status entry.
        {
            var backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup != null)
            {
                backups.Remove(backup);
                statusManager.RemoveStatus(name);
            }
        }

        private bool CanExecuteBackup()
        // Out: bool
        // Description: Checks whether any software package is running that would block execution.
        {
            var settings = BackupRulesManager.Instance;
            bool isSoftwareRunning = settings.IsAnyBusinessSoftwareRunning();

            if (isSoftwareRunning)
            {
                string? runningSoftware = settings.GetRunningBusinessSoftware();
                Console.WriteLine($"Le logiciel métier '{runningSoftware}' est en cours d'exécution. La sauvegarde ne peut pas être lancée.");
                return false;
            }

            return true;
        }
        public void ExecuteBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Executes a single backup by name if it is valid and not currently running.
        {
            Backup? backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup == null) return;

            if (runningJobs.TryGetValue(name, out bool isRunning) && isRunning)
            {
                Console.WriteLine($"La sauvegarde '{name}' est déjà en cours d'exécution.");
                return;
            }

            if (!currentJobAllowedToComplete && !CanExecuteBackup())
            {
                Console.WriteLine($"Sauvegarde '{name}' annulée : logiciel métier détecté.");
                return;
            }

            var existingStatus = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
            if (existingStatus != null)
            {
                existingStatus.LastBackupDate = DateTime.Now;
                existingStatus.State = "ACTIVE";
                existingStatus.Progression = 0;
                statusManager.UpdateStatus(existingStatus);
            }

            CancellationTokenSource? cts = null;
            backupCancellationTokens.TryGetValue(name, out cts);
            if (cts == null || cts.IsCancellationRequested)
            {
                cts = new CancellationTokenSource();
                backupCancellationTokens[name] = cts;
            }

            runningJobs[name] = true;

            backup.State = BackupJobState.RUNNING;

            BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));

            try
            {
                backup.Play();
                if (backup.Type == "FULL")
                {
                    var statusManager = new StatusManager();
                    var logObserver = new LogObserver();
                    new CompleteBackupStrategy().MakeBackup(backup, statusManager, logObserver, cts.Token);
                    Console.WriteLine($"Sauvegarde complète '{backup.Name}' terminée avec succès");
                }
                else if (backup.Type == "DIFFERENTIAL")
                {
                    var statusManager = new StatusManager();
                    var logObserver = new LogObserver();
                    new IncrementalBackupStrategy().MakeBackup(backup, statusManager, logObserver, cts.Token);
                    Console.WriteLine($"Sauvegarde différentielle '{backup.Name}' terminée avec succès");
                }
                else
                {
                    Console.WriteLine($"Type de sauvegarde '{backup.Type}' non reconnu.");
                }

                var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                if (status != null)
                {
                    status.LastBackupDate = DateTime.Now;
                    status.State = "COMPLETED";
                    status.Progression = 100;
                    statusManager.UpdateStatus(status);
                }

                backup.State = BackupJobState.COMPLETED;

                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Sauvegarde '{name}' annulée.");

                backup.State = BackupJobState.STOPPED;

                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'exécution de la sauvegarde '{name}': {ex.Message}");

                backup.State = BackupJobState.STOPPED;

                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
            }
            finally
            {
                runningJobs[name] = false;
            }
        }

        public async Task ExecuteAllBackupsAsync(bool parallel = false, int maxConcurrency = 3)
        // In: parallel (bool), maxConcurrency (int)
        // Out: Task
        // Description: Executes all backups either sequentially or in parallel with optional concurrency limit.
        {
            if (!CanExecuteBackup())
            {
                Console.WriteLine("Impossible d'exécuter les sauvegardes : un logiciel métier est en cours d'exécution.");
                return;
            }

            PriorityFileManager.Instance.Reset();

            currentJobAllowedToComplete = true;

            if (!parallel)
            {
                foreach (var backup in backups)
                {
                    if (!currentJobAllowedToComplete)
                    {
                        break;
                    }
                    ExecuteBackup(backup.Name);
                }
            }
            else
            {
                using SemaphoreSlim semaphore = new(maxConcurrency);
                var tasks = backups.Select(async backup =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await Task.Run(() => ExecuteBackup(backup.Name));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            currentJobAllowedToComplete = false;
        }

        public void ExecuteAllBackups()
        // Out: void
        // Description: Executes all backups sequentially.
        {
            ExecuteAllBackupsAsync(false).Wait();
        }

        public List<Backup> GetAllBackup()
        // Out: List<Backup>
        // Description: Returns all current backup definitions.
        {
            return new List<Backup>(backups);
        }

        private void CleanOrphanStatuses()
        // Out: void
        // Description: Removes status entries that do not match any existing backup.
        {
            var allStatuses = statusManager.GetAllStatuses();
            var existingNames = backups.Select(b => b.Name).ToHashSet();

            foreach (var status in allStatuses)
            {
                if (!existingNames.Contains(status.Name))
                {
                    statusManager.RemoveStatus(status.Name);
                }
            }
        }
    }

    public class BackupPausedEventArgs : EventArgs
    // Description: Event arguments for when a backup is paused due to business software activity.
    {
        public string BackupName { get; }
        public string SoftwareName { get; }

        public BackupPausedEventArgs(string backupName, string softwareName)
        // In: backupName (string), softwareName (string)
        // Out: /
        // Description: Initializes a new instance with the backup and software name.
        {
            BackupName = backupName;
            SoftwareName = softwareName;
        }
    }

    public class BackupResumedEventArgs : EventArgs
    // Description: Event arguments for when a paused backup is resumed.
    {
        public string BackupName { get; }
        public string SoftwareName { get; }

        public BackupResumedEventArgs(string backupName, string softwareName)
        // In: backupName (string), softwareName (string)
        // Out: /
        // Description: Initializes a new instance with the backup and software name.
        {
            BackupName = backupName;
            SoftwareName = softwareName;
        }
    }

    public class BackupStateChangedEventArgs : EventArgs
    // Description: Event arguments for when the backup state changes.
    {
        public string BackupName { get; }
        public BackupJobState NewState { get; }

        public BackupStateChangedEventArgs(string backupName, BackupJobState newState)
        // In: backupName (string), newState (BackupJobState)
        // Out: /
        // Description: Initializes a new instance with the backup name and new state.
        {
            BackupName = backupName;
            NewState = newState;
        }
    }
}
