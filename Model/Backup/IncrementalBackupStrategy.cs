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
            // Set the backup state to RUNNING at the start
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;
                Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
                Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionManager.EncryptionExecutablePath}");

                // Check for cancellation before starting
                backup.CheckPauseAndCancellation();

                DateTime lastBackupTime = statusManager.GetLastBackupDate(backup.Name);

                string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int totalFiles = files.Length;
                List<string> copiedFiles = new();

                // Register priority files with the manager
                PriorityFileManager.Instance.RegisterPriorityFiles(backup.Name, backup.SourceDirectory);

                // Get priority extensions and separate files
                var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions ?? new List<string>();
                var priorityFiles = files
                    .Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower()) &&
                               (File.GetLastWriteTime(f) > lastBackupTime || !File.Exists(Path.Combine(backup.TargetDirectory, Path.GetRelativePath(backup.SourceDirectory, f)))))
                    .ToList();
                var nonPriorityFiles = files
                    .Where(f => !priorityExtensions.Contains(Path.GetExtension(f).ToLower()) &&
                               (File.GetLastWriteTime(f) > lastBackupTime || !File.Exists(Path.Combine(backup.TargetDirectory, Path.GetRelativePath(backup.SourceDirectory, f)))))
                    .ToList();

                // Process priority files first
                foreach (var file in priorityFiles)
                {
                    // Check if paused or cancelled before processing each file
                    backup.CheckPauseAndCancellation();

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

                    // Update backup progress
                    backup.Progress = $"{(int)((copiedFiles.Count / (double)totalFiles) * 100)}%";

                    // Ajouter un délai de 2 secondes pour permettre de voir l'état ACTIVE
                    Thread.Sleep(2000);

                    // Check again for pause/cancel after the delay
                    backup.CheckPauseAndCancellation();

                    string ext = Path.GetExtension(file).ToLower();
                    bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);

                    int encryptionTime = 0;
                    int transferTime = 0;

                    bool isLargeFile = LargeFileTransferManager.Instance.IsFileLarge(file, BackupRulesManager.Instance.LargeFileSizeThresholdKB);

                    if (isLargeFile)
                    {
                        LargeFileTransferManager.Instance.WaitForLargeFileTransferAsync().Wait();
                    }

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        File.Copy(file, destinationPath, true);
                        sw.Stop();
                        transferTime = (int)sw.Elapsed.TotalMilliseconds;
                        copiedFiles.Add(file);
                    }
                    finally
                    {
                        if (isLargeFile)
                        {
                            LargeFileTransferManager.Instance.ReleaseLargeFileTransfer();
                        }
                    }

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
                }

                // Mark this backup as having processed its priority files
                PriorityFileManager.Instance.MarkBackupAsProcessing(backup.Name);

                // Only process non-priority files if allowed
                if (PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                {
                    foreach (var file in nonPriorityFiles)
                    {
                        // Vérifier s'il y a de nouveaux fichiers prioritaires
                        PriorityFileManager.Instance.CheckAndUpdatePriorityStatus(backup.Name, backup.SourceDirectory);
                        
                        // Si on ne peut plus traiter les fichiers non prioritaires, on sort de la boucle
                        if (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                        {
                            Console.WriteLine($"Sauvegarde '{backup.Name}' mise en pause : des fichiers prioritaires ont été détectés dans une autre sauvegarde.");
                            break;
                        }

                        // Check if paused or cancelled before processing each file
                        backup.CheckPauseAndCancellation();

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
                            totalFiles - copiedFiles.Count,
                            (int)((copiedFiles.Count / (double)totalFiles) * 100),
                            DateTime.Now
                        );
                        statusManager.UpdateStatus(statusEntry);

                        backup.Progress = $"{(int)((copiedFiles.Count / (double)totalFiles) * 100)}%";

                        Thread.Sleep(2000);
                        backup.CheckPauseAndCancellation();

                        string ext = Path.GetExtension(file).ToLower();
                        bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);

                        int encryptionTime = 0;
                        int transferTime = 0;

                        bool isLargeFile = LargeFileTransferManager.Instance.IsFileLarge(file, BackupRulesManager.Instance.LargeFileSizeThresholdKB);

                        if (isLargeFile)
                        {
                            LargeFileTransferManager.Instance.WaitForLargeFileTransferAsync().Wait();
                        }

                        try
                        {
                            var sw = Stopwatch.StartNew();
                            File.Copy(file, destinationPath, true);
                            sw.Stop();
                            transferTime = (int)sw.Elapsed.TotalMilliseconds;
                            copiedFiles.Add(file);
                        }
                        finally
                        {
                            if (isLargeFile)
                            {
                                LargeFileTransferManager.Instance.ReleaseLargeFileTransfer();
                            }
                        }

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
                    }
                }

                // Clean up priority file tracking
                PriorityFileManager.Instance.RemoveBackup(backup.Name);

                // Set final state
                backup.State = Easy_Save.Model.Enum.BackupJobState.COMPLETED;
                backup.Progress = "100%";
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                Console.WriteLine($"Backup job '{backup.Name}' was stopped by the user.");
                backup.State = Easy_Save.Model.Enum.BackupJobState.STOPPED;
            }
            catch (Exception ex)
            {
                // Handle other errors
                Console.WriteLine($"Error during backup: {ex.Message}");
                backup.State = Easy_Save.Model.Enum.BackupJobState.ERROR;
                throw;
            }
        }
    }
}
