using System;
using System.Collections.Generic;
using System.IO;
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
            if (!Directory.Exists(src))
            {
                Console.WriteLine($"The folder \"{src}\" doesn't exist");
                return;
            }

            if (!Directory.Exists(dest))
            {
                try
                {
                    Directory.CreateDirectory(dest);
                    Console.WriteLine($"Folder \"{dest}\" created");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Impossible to create folder : {ex.Message}");
                    return;
                }
            }

            var backup = new Backup
            {
                Name = name,
                SourceDirectory = src,
                TargetDirectory = dest,
                Type = type,
                Progress = "0%"
            };

            try
            {
                backupManager.AddBackup(backup);
                Console.WriteLine($"Backup created : {name}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Impossible to create backup : {ex.Message}");
            }
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
            Console.WriteLine($"Backup deleted : {name}");
        }
    }
}