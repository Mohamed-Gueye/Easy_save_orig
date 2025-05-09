using System;
using System.Collections.Generic;
using Easy_Save.Model;

namespace Easy_Save.Controller
{
    public class BackupProcess
    {
        private readonly BackupManager backupManager;

        public BackupProcess()
        {
            backupManager = new BackupManager();
        }

        public void CreateBackup(string name, string src, string dest, string type)
        {
            var backup = new Backup
            {
                Name = name,
                SourceDirectory = src,
                TargetDirectory = dest,
                Type = type,
                Progress = "0%"
            };

            backupManager.AddBackup(backup);
            Console.WriteLine($"Backup créé : {name}");
        }

        public void ExecuteBackup(string name)
        {
            backupManager.ExecuteBackup(name);
        }

        public void RunAllBackups()
        {
            backupManager.ExecuteAllBackups();
        }

        public List<Backup> GetAllBackup()
        {
            return backupManager.GetAllBackup();
        }

        public void DeleteBackup(string name)
        {
            backupManager.RemoveBackup(name);
            Console.WriteLine($"Backup supprimé : {name}");
        }
    }
}
