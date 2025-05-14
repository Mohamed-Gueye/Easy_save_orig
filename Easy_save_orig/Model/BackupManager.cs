using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                // Nettoyage associé : statut et autres dépendances
                statusManager.RemoveStatus(name);
                // Si d'autres observateurs ou ressources sont attachés, les libérer ici
            }
        }

        public void ExecuteBackup(string name)
        {
            Backup? backup = backups.FirstOrDefault(b => b.Name == name);
            if (backup == null)
            {
                Console.WriteLine("No backup found");
                return;
            }

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

            strategy.MakeBackup(backup);
            Console.WriteLine($"Backup finished : {backup.Name}");
        }

        public void ExecuteAllBackups()
        {
            foreach (var backup in backups)
            {
                ExecuteBackup(backup.Name);
            }
        }

        public List<Backup> GetAllBackup()
        {
            return new List<Backup>(backups); // copie pour éviter modifications externes
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
