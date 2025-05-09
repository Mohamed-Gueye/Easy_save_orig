using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
                Console.WriteLine("Aucun backup trouvé avec ce nom.");
                return;
            }

            if (!Directory.Exists(backup.TargetDirectory))
            {
                Directory.CreateDirectory(backup.TargetDirectory);
            }

            strategy = backup.Type == "full"
                ? new CompleteBackupStrategy()
                : backup.Type == "differential"
                    ? new IncrementalBackupStrategy()
                    : throw new InvalidOperationException("Type de sauvegarde invalide.");

            string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int totalFiles = files.Length;
            int filesDone = 0;

            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                string destFile = Path.Combine(backup.TargetDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

                var statusEntry = new Model.Status.StatusEntry(
                    backup.Name,
                    file,
                    destFile,
                    "ACTIVE",
                    totalFiles,
                    totalSize,
                    totalFiles - filesDone,
                    (int)((filesDone / (double)totalFiles) * 100)
                );
                statusManager.UpdateStatus(statusEntry);

                Stopwatch sw = Stopwatch.StartNew();
                File.Copy(file, destFile, true);
                sw.Stop();

                long fileSize = new FileInfo(file).Length;
                double time = sw.Elapsed.TotalMilliseconds;

         
                logObserver.Update(backup, fileSize, time);

                Thread.Sleep(2000);
                filesDone++;
            }

           
            var finalStatus = new Model.Status.StatusEntry(
                backup.Name,
                backup.SourceDirectory,
                backup.TargetDirectory,
                "END",
                totalFiles,
                totalSize,
                0,
                100
            );
            statusManager.UpdateStatus(finalStatus);

            Console.WriteLine($"Sauvegarde terminée : {backup.Name}");
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
    }
}
