using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public void ExecuteBackup(string name)
        {
            Backup? backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup == null) return;

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

        public async Task ExecuteAllBackupsAsync(bool isConcurrent = false, int maxConcurrency = 4)
        {
            if (!isConcurrent)
            {
                foreach (var backup in backups)
                {
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
