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
        private bool currentJobAllowedToComplete = true;
        private readonly ConcurrentDictionary<string, bool> runningJobs = new ConcurrentDictionary<string, bool>();

        public BackupManager()
        // Description: Initializes the BackupManager and loads status/log observers.
        {
            statusManager = new StatusManager();
            logObserver = new LogObserver();

            CleanOrphanStatuses();
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

            runningJobs[name] = true;
            
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
                runningJobs.TryRemove(name, out _);
            }
        }

        public async Task ExecuteAllBackupsAsync(bool isConcurrent = false, int maxConcurrency = 4)
        // In: isConcurrent (bool), maxConcurrency (int)
        // Out: Task
        // Description: Executes all backups concurrently with optional concurrency limit.
        {
            if (!CanExecuteBackup())
            {
                Console.WriteLine("Toutes les sauvegardes ont été annulées : logiciel métier détecté.");
                return;
            }

            currentJobAllowedToComplete = true;

            var backupQueue = new Queue<Backup>(backups);

            if (!isConcurrent)
            {
                while (backupQueue.Count > 0)
                {
                    var backup = backupQueue.Dequeue();
                    
                    if (backupQueue.Count > 0 && !CanExecuteBackup())
                    {
                        Console.WriteLine("Les sauvegardes restantes ont été annulées : logiciel métier détecté.");
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
