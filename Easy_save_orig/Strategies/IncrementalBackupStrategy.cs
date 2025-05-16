using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Easy_Save.Interfaces;
using Easy_Save.Model;
using Easy_Save.Model.IO;
using Easy_Save.Model.Status;
using Easy_Save.Model.Observer;
using System.Diagnostics;
using System.Threading;

namespace Easy_Save.Strategies
{
    public class IncrementalBackupStrategy : IBackupStrategy
    {
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver)
        {
            var encryptionConfig = EncryptionSettings.Load("config.json");
            Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionConfig.extensionsToEncrypt)}");
            Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionConfig.encryptionExecutablePath}");

            DateTime lastBackupTime = statusManager.GetLastBackupDate(backup.Name);

            string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int totalFiles = files.Length;
            List<string> copiedFiles = new();

            foreach (string file in files)
            {
                DateTime lastModified = File.GetLastWriteTime(file);

                if (lastModified > lastBackupTime)
                {
                    Console.WriteLine($"Fichier détecté : {file}");

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    string? destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // Créer une entrée de statut ACTIVE
                    var statusEntry = new StatusEntry(
                        backup.Name,
                        file,
                        destinationPath,
                        "ACTIVE",
                        totalFiles,
                        totalSize,
                        totalFiles - copiedFiles.Count,
                        (int)((copiedFiles.Count / (double)totalFiles) * 100),
                        DateTime.Now
                    );
                    statusManager.UpdateStatus(statusEntry);
                    
                    // Ajouter un délai de 2 secondes pour permettre de voir l'état ACTIVE
                    Thread.Sleep(2000);

                    string ext = Path.GetExtension(file).ToLower();
                    bool shouldEncrypt = encryptionConfig.extensionsToEncrypt
                        .Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));

                    int encryptionTime = 0;
                    int transferTime = 0;

                    var swa = Stopwatch.StartNew();
                    File.Copy(file, destinationPath, true);
                    swa.Stop();
                    transferTime = (int)swa.Elapsed.TotalMilliseconds;

                    if (shouldEncrypt)
                    {
                        encryptionTime = EncryptionHelper.EncryptFile(destinationPath, encryptionConfig.key, encryptionConfig.encryptionExecutablePath);

                        if (encryptionTime >= 0)
                        {
                            string decryptedPath = Path.Combine(
                                Path.GetDirectoryName(destinationPath)!,
                                Path.GetFileNameWithoutExtension(destinationPath) + "_decrypted" + Path.GetExtension(destinationPath)
                            );

                            File.Copy(destinationPath, decryptedPath, true);
                            int decryptTime = EncryptionHelper.EncryptFile(decryptedPath, encryptionConfig.key, encryptionConfig.encryptionExecutablePath);

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
                    copiedFiles.Add(file);
                }
            }

            long finalSize = copiedFiles.Sum(f => new FileInfo(f).Length);
            statusManager.UpdateStatus(new StatusEntry(
                backup.Name,
                backup.SourceDirectory,
                backup.TargetDirectory,
                "END",
                totalFiles,
                finalSize,
                0,
                100,
                DateTime.Now
            ));
        }
    }
}
