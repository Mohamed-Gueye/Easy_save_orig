using System;
using Easy_Save.Interfaces;
using Easy_Save.Model;

namespace Easy_Save.Strategies;
public class IncrementalBackupStrategy : IBackupStrategy
{
    public void MakeBackup(Backup backup)
    {
        string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
        DateTime lastBackupTime = DateTime.Now.AddDays(-1); // TO replace with a real last backup time

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

                File.Copy(file, destinationPath, overwrite: true);
            }
        }

        Console.WriteLine($"Copie différentielle de \"{backup.Name}\" terminée !");
    }
}
