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
            }
        }

        private void OnBusinessSoftwareStopped(object sender, string softwareName)
        {
            // Reprendre toutes les sauvegardes en pause
            foreach (var job in pausedJobs.Where(j => j.Value))
            {
                ResumeBackup(job.Key);
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
                    backup.Resume();
                    pausedJobs[name] = false;
                    
                    // Mettre à jour le statut
                    var status = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == name);
                    if (status != null)
                    {
                        status.State = "ACTIVE";
                        statusManager.UpdateStatus(status);
                    }
                }
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
                existingStatus.State = "READY";
                existingStatus.Progression = 0;
                existingStatus.NbFilesLeftToDo = existingStatus.TotalFilesToCopy;
                statusManager.UpdateStatus(existingStatus);
            }

            // Réinitialiser l'état de l'objet Backup également
            backup.Reset();

            runningJobs[name] = true;
            pausedJobs[name] = false;

            try
            {
                if (!Directory.Exists(backup.TargetDirectory))
                {
                    Directory.CreateDirectory(backup.TargetDirectory);
                }

                IBackupStrategy strategy = backup.Type.Trim().ToLower() switch
                {
                    "full" => new CompleteBackupStrategy(),
                    "differential" => new IncrementalBackupStrategy(),
                    _ => throw new InvalidOperationException("Invalid backup type.")
                };

                strategy.MakeBackup(backup, statusManager, logObserver);
            }
            finally
            {
                runningJobs[name] = false;
                pausedJobs[name] = false;
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
}
