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
        public void MakeBackup(Backup backup)
        {
            var encryptionConfig = EncryptionSettings.Load("config.json");
            Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionConfig.extensionsToEncrypt)}");
            Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionConfig.encryptionExecutablePath}");

            var statusManager = new StatusManager();
            var logObserver = new LogObserver();
            DateTime lastBackupTime = statusManager.GetLastBackupDate(backup.Name);

            string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
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

                    string ext = Path.GetExtension(file).ToLower();
                    bool shouldEncrypt = encryptionConfig.extensionsToEncrypt
                        .Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));

                    int encryptionTime = 0;

                    if (shouldEncrypt)
                    {
                        File.Copy(file, destinationPath, true);
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
                            {
                                Console.WriteLine($"Déchiffrement effectué → {decryptedPath}");
                            }
                            else
                            {
                                Console.WriteLine($"Erreur de déchiffrement pour : {decryptedPath}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Erreur de chiffrement : {destinationPath} (code {encryptionTime})");
                            continue;
                        }
                    }
                    else
                    {
                        File.Copy(file, destinationPath, true);
                    }

                    logObserver.Update(backup, new FileInfo(file).Length, encryptionTime);
                    copiedFiles.Add(file);
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

            Console.WriteLine("Backup completed successfully.");
        }
    }
}
