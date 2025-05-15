using System;
using System.Collections.Generic;
using System.Diagnostics;
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

                    string ext = Path.GetExtension(file).ToLower();
                    bool shouldEncrypt = encryptionConfig.extensionsToEncrypt
                        .Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));

                    int encryptionTime = 0;
                    int transferTime = 0;

                    var sw = Stopwatch.StartNew();
                    File.Copy(file, destinationPath, true);
                    sw.Stop();
                    transferTime = (int)sw.Elapsed.TotalMilliseconds;

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

            Console.WriteLine("Backup completed successfully.");
        }
    }
}
