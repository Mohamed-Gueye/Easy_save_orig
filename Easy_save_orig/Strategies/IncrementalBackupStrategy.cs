using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Easy_Save.Interfaces;
using Easy_Save.Model;
using Easy_Save.Model.IO;
using Easy_Save.Model.Status;
using Easy_Save.Model.Observer;

namespace Easy_Save.Strategies
{
    public class IncrementalBackupStrategy : IBackupStrategy
    {
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver)
        {
            DateTime lastBackupTime = statusManager.GetLastBackupDate(backup.Name);

            string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
            List<string> copiedFiles = new();

            foreach (string file in files)
            {
                DateTime lastModified = File.GetLastWriteTime(file);

                if (lastModified > lastBackupTime)
                {
                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);

                    string? destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    File.Copy(file, destinationPath, overwrite: true);
                    sw.Stop();

                    copiedFiles.Add(file);

                    long fileSize = new FileInfo(file).Length;
                    double transferTime = sw.Elapsed.TotalMilliseconds;
                    logObserver.Update(backup, fileSize, transferTime);
                }
            }

            long totalSize = copiedFiles.Sum(f => new FileInfo(f).Length);
            int totalCopied = copiedFiles.Count;

            statusManager.UpdateStatus(new StatusEntry(
                backup.Name,
                backup.SourceDirectory,
                backup.TargetDirectory,
                "END",
                totalCopied,
                totalSize,
                0,
                100,
                DateTime.Now
            ));
        }
    }
}