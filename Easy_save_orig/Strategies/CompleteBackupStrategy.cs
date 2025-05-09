using System;
using Easy_Save.Interfaces;
using Easy_Save.Model;

namespace Easy_Save.Strategies;
public class CompleteBackupStrategy : IBackupStrategy
{
    public void MakeBackup(Backup backup)
    {
        string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);

        foreach (string file in files)
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

        Console.WriteLine($"Copie complète de \"{backup.Name}\" terminée !");
    }
}