using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Easy_Save.Interfaces;
using Easy_Save.Model;
using Easy_Save.Model.Status;
using Easy_Save.Model.IO;
using Easy_Save.Model.Observer;

namespace Easy_Save.Strategies
{
    public class CompleteBackupStrategy : IBackupStrategy
    {
        public void MakeBackup(Backup backup)
        {
            var statusManager = new StatusManager();
            var logObserver = new LogObserver();

            string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int totalFiles = files.Length;
            int filesDone = 0;

            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                string? destinationDir = Path.GetDirectoryName(destinationPath);

                if (!Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                var statusEntry = new StatusEntry(
                    backup.Name,
                    file,
                    destinationPath,
                    "ACTIVE",
                    totalFiles,
                    totalSize,
                    totalFiles - filesDone,
                    (int)((filesDone / (double)totalFiles) * 100),
                    DateTime.Now
                );
                statusManager.UpdateStatus(statusEntry);

                Stopwatch sw = Stopwatch.StartNew();
                File.Copy(file, destinationPath, true);
                sw.Stop();

                long fileSize = new FileInfo(file).Length;
                double transferTime = sw.Elapsed.TotalMilliseconds;

                logObserver.Update(backup, fileSize, transferTime);

                filesDone++;
            }

            var finalStatus = new StatusEntry(
                backup.Name,
                backup.SourceDirectory,
                backup.TargetDirectory,
                "END",
                totalFiles,
                totalSize,
                0,
                100,
                DateTime.Now
            );
            statusManager.UpdateStatus(finalStatus);

            Console.WriteLine(translationProcess.GetTranslation("backup.done"));
        }
    }
}
