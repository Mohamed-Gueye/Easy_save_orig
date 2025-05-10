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
        private const int MAX_BACKUPS = 5;
        private List<Backup> backups = new();
        private IBackupStrategy? strategy;

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
            if (backups.Count >= MAX_BACKUPS)
                throw new InvalidOperationException("Maximum backup jobs reached.");

            backups.Add(backup);
        }

        public void RemoveBackup(string name)
        {
            backups.RemoveAll(b => b.Name == name);
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

            strategy = backup.Type.Trim().ToLower() switch
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
            return new List<Backup>(backups);
        }

        private void CleanOrphanStatuses()
        {
            var allStatuses = statusManager.GetAllStatuses();
            var existingNames = backups.Select(b => b.Name).ToHashSet();

            bool changed = false;
            foreach (var status in allStatuses)
            {
                if (!existingNames.Contains(status.Name))
                {
                    statusManager.RemoveStatus(status.Name);
                    changed = true;
                }
            }
        }
    }
}
