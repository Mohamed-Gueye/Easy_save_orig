using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Easy_Save.Interfaces;
using Easy_Save.Model;
using Easy_Save.Model.Status;
using Easy_Save.Model.IO;
using Easy_Save.Model.Observer;

namespace Easy_Save.Strategies
{
    public class CompleteBackupStrategy : IBackupStrategy
    {
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver)
        // In: backup (Backup), statusManager (StatusManager), logObserver (LogObserver)
        // Out: void
        // Description: Executes a full backup by copying all files from source to destination.
        {
            var encryptionManager = EncryptionManager.Instance;
            Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
            Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionManager.EncryptionExecutablePath}");


            string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int totalFiles = files.Length;
            int filesDone = 0;

            foreach (string file in files)
            {
                Console.WriteLine($"Fichier détecté : {file}");

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
                
                // Ajouter un délai de 2 secondes pour permettre de voir l'état ACTIVE
                Thread.Sleep(2000);

                string ext = Path.GetExtension(file).ToLower();
                bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);

                int encryptionTime = 0;
                int transferTime = 0;

                var sw = Stopwatch.StartNew();
                File.Copy(file, destinationPath, true);
                sw.Stop();
                transferTime = (int)sw.Elapsed.TotalMilliseconds;

                if (shouldEncrypt)
                {
                    encryptionTime = encryptionManager.EncryptFile(destinationPath);

                    if (encryptionTime >= 0)
                    {
                        string decryptedPath = Path.Combine(
                            Path.GetDirectoryName(destinationPath)!,
                            Path.GetFileNameWithoutExtension(destinationPath) + "_decrypted" + Path.GetExtension(destinationPath)
                        );

                        File.Copy(destinationPath, decryptedPath, true);
                        int decryptTime = encryptionManager.EncryptFile(decryptedPath);

                        if (decryptTime >= 0)
                            Console.WriteLine($"Déchiffrement effectué → {decryptedPath}");
                        else
                            Console.WriteLine($"Erreur de déchiffrement pour : {decryptedPath}");
                    }
                    else
                    {
                        Console.WriteLine($"Erreur de chiffrement : {destinationPath} (code {encryptionTime})");
                        continue;
                    }
                }

                logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
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

            Console.WriteLine("Backup completed successfully.");
        }
    }
}
