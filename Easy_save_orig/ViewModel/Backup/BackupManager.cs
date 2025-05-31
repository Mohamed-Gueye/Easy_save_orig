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

        // Événements pour notifier l'interface utilisateur
        public event EventHandler<BackupPausedEventArgs>? BackupPaused;
        public event EventHandler<BackupResumedEventArgs>? BackupResumed;
        public event EventHandler<BackupStateChangedEventArgs>? BackupStateChanged;

        public BackupManager()
        // Description: Initializes the BackupManager and loads status/log observers.
        {
            statusManager = new StatusManager();
            logObserver = new LogObserver();
            processWatcher = ProcessWatcher.Instance;

            // S'abonner aux événements du ProcessWatcher
            processWatcher.BusinessSoftwareStarted += OnBusinessSoftwareStarted;
            processWatcher.BusinessSoftwareStopped += OnBusinessSoftwareStopped;

            // Démarrer la surveillance des processus
            processWatcher.StartWatching();

            CleanOrphanStatuses();
        }

        private void OnBusinessSoftwareStarted(object sender, string softwareName)
        {
            // Mettre en pause toutes les sauvegardes en cours
            foreach (var job in runningJobs.Where(j => j.Value))
            {
                PauseBackup(job.Key);
                // Déclencher l'événement pour notifier l'interface utilisateur
                BackupPaused?.Invoke(this, new BackupPausedEventArgs(job.Key, softwareName));
            }
        }

        private void OnBusinessSoftwareStopped(object sender, string softwareName)
        {
            // Reprendre toutes les sauvegardes en pause
            foreach (var job in pausedJobs.Where(j => j.Value))
            {
                ResumeBackup(job.Key);
                // Déclencher l'événement pour notifier l'interface utilisateur
                BackupResumed?.Invoke(this, new BackupResumedEventArgs(job.Key, softwareName));
            }
        }

        public void PauseBackup(string name)
        {
            if (runningJobs.TryGetValue(name, out bool isRunning) && isRunning)
            {
                var backup = backups.FirstOrDefault(b => b.Name == name);
                if (backup != null)
                {
                    backup.Pause();
                    pausedJobs[name] = true;

                    // Mettre à jour le statut
                    var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                    if (status != null)
                    {
                        status.State = "PAUSED";
                        statusManager.UpdateStatus(status);
                    }

                    // Déclencher l'événement de changement d'état
                    BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
                }
            }
        }
        public void ResumeBackup(string name)
        {
            if (pausedJobs.TryGetValue(name, out bool isPaused) && isPaused)
            {
                var backup = backups.FirstOrDefault(b => b.Name == name);
                if (backup != null && !BackupRulesManager.Instance.IsAnyBusinessSoftwareRunning())
                {
                    // Utiliser Play() au lieu de Resume() pour changer l'état correctement
                    backup.Play();
                    pausedJobs[name] = false;

                    // Mettre à jour le statut
                    var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                    if (status != null)
                    {
                        status.State = "ACTIVE";
                        statusManager.UpdateStatus(status);
                    }

                    // Déclencher les événements de changement d'état
                    BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
                    BackupResumed?.Invoke(this, new BackupResumedEventArgs(name, "Manual resume"));
                }
            }
        }

        public void StopBackup(string name)
        {
            var backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup != null)
            {
                // Arrêter la sauvegarde via la méthode Stop()
                backup.Stop();

                // Annuler également via le CancellationTokenSource
                if (backupCancellationTokens.TryGetValue(name, out var tokenSource))
                {
                    tokenSource.Cancel();
                }

                // Mettre à jour les états
                runningJobs[name] = false;
                pausedJobs[name] = false;

                // Mettre à jour le statut
                var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                if (status != null)
                {
                    status.State = "STOPPED";
                    statusManager.UpdateStatus(status);
                }

                // Déclencher l'événement de changement d'état
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

            // Réinitialiser l'état de la sauvegarde dans le StatusManager avant de commencer
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

            // Mark the job as running
            runningJobs[name] = true;

            // Mettre à jour l'état de la sauvegarde
            backup.State = BackupJobState.RUNNING;

            // Déclencher l'événement de changement d'état
            BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));

            // Execute the backup
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

                // Mettre à jour le statut après l'exécution réussie
                var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                if (status != null)
                {
                    status.LastBackupDate = DateTime.Now;
                    status.State = "COMPLETED";
                    status.Progression = 100;
                    statusManager.UpdateStatus(status);
                }

                // Mettre à jour l'état de la sauvegarde
                backup.State = BackupJobState.COMPLETED;

                // Déclencher l'événement de changement d'état pour la complétion
                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Sauvegarde '{name}' annulée.");

                // Si la sauvegarde a été annulée, mettre à jour son état
                backup.State = BackupJobState.STOPPED;

                // Déclencher l'événement de changement d'état pour l'annulation
                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'exécution de la sauvegarde '{name}': {ex.Message}");

                // En cas d'erreur, considérer la sauvegarde comme arrêtée
                backup.State = BackupJobState.STOPPED;

                // Déclencher l'événement de changement d'état pour l'erreur
                BackupStateChanged?.Invoke(this, new BackupStateChangedEventArgs(name, backup.State));
            }
            finally
            {
                // Mark the job as not running
                runningJobs[name] = false;
            }
        }

        public async Task ExecuteAllBackupsAsync(bool parallel = false, int maxConcurrency = 3)
        {
            if (!CanExecuteBackup())
            {
                Console.WriteLine("Impossible d'exécuter les sauvegardes : un logiciel métier est en cours d'exécution.");
                return;
            }

            // Reset the priority file manager before starting all backups
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

    // Classes d'événements pour notifier l'interface utilisateur
    public class BackupPausedEventArgs : EventArgs
    {
        public string BackupName { get; }
        public string SoftwareName { get; }

        public BackupPausedEventArgs(string backupName, string softwareName)
        {
            BackupName = backupName;
            SoftwareName = softwareName;
        }
    }

    public class BackupResumedEventArgs : EventArgs
    {
        public string BackupName { get; }
        public string SoftwareName { get; }

        public BackupResumedEventArgs(string backupName, string softwareName)
        {
            BackupName = backupName;
            SoftwareName = softwareName;
        }
    }

    public class BackupStateChangedEventArgs : EventArgs
    {
        public string BackupName { get; }
        public BackupJobState NewState { get; }

        public BackupStateChangedEventArgs(string backupName, BackupJobState newState)
        {
            BackupName = backupName;
            NewState = newState;
        }
    }
}
