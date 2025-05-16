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
        {
            statusManager = new StatusManager();
            logObserver = new LogObserver();

            CleanOrphanStatuses();
        }

        public void AddBackup(Backup backup)
        {
            if (backup == null)
                throw new ArgumentNullException(nameof(backup));

            backups.Add(backup);
        }

        public void RemoveBackup(string name)
        {
            var backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup != null)
            {
                backups.Remove(backup);
                statusManager.RemoveStatus(name);
            }
        }

        /// <summary>
        /// Vérifie si un logiciel métier est en cours d'exécution et bloque le démarrage des sauvegardes
        /// </summary>
        /// <returns>True si l'exécution de la sauvegarde est autorisée, False sinon</returns>
        private bool CanExecuteBackup()
        {
            var settings = BusinessSettings.Instance;
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
        {
            // Vérifier si une sauvegarde avec ce nom existe
            Backup? backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup == null) return;

            // Si le job est déjà en cours d'exécution, ne pas le relancer
            if (runningJobs.TryGetValue(name, out bool isRunning) && isRunning)
            {
                Console.WriteLine($"La sauvegarde '{name}' est déjà en cours d'exécution.");
                return;
            }

            // Vérifier si le logiciel métier est en cours d'exécution
            if (!currentJobAllowedToComplete && !CanExecuteBackup())
            {
                Console.WriteLine($"Sauvegarde '{name}' annulée : logiciel métier détecté.");
                return;
            }

            // Marquer le job comme en cours d'exécution
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
                // Marquer le job comme terminé
                runningJobs.TryRemove(name, out _);
            }
        }

        public async Task ExecuteAllBackupsAsync(bool isConcurrent = false, int maxConcurrency = 4)
        {
            // Vérifier si le logiciel métier est en cours d'exécution avant de commencer
            if (!CanExecuteBackup())
            {
                Console.WriteLine("Toutes les sauvegardes ont été annulées : logiciel métier détecté.");
                return;
            }

            // Autoriser la poursuite des sauvegardes actuelles même si le logiciel est démarré pendant l'exécution
            currentJobAllowedToComplete = true;

            // Créer une file d'attente pour les sauvegardes
            var backupQueue = new Queue<Backup>(backups);

            if (!isConcurrent)
            {
                while (backupQueue.Count > 0)
                {
                    var backup = backupQueue.Dequeue();
                    
                    // Pour le mode séquentiel, vérifier le logiciel métier entre chaque sauvegarde
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
            
            // Désactiver l'autorisation de poursuite après la fin de toutes les sauvegardes
            currentJobAllowedToComplete = false;
        }

        public void ExecuteAllBackups()
        {
            ExecuteAllBackupsAsync(false).Wait();
        }

        public List<Backup> GetAllBackup()
        {
            return new List<Backup>(backups);
        }

        private void CleanOrphanStatuses()
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
