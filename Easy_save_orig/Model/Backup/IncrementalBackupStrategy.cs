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
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver, CancellationToken cancellationToken = default)
        {
            // Set the backup state to RUNNING at the start
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;
                Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
                Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionManager.EncryptionExecutablePath}");

                // Check for cancellation before starting
                cancellationToken.ThrowIfCancellationRequested();
                backup.CheckPauseAndCancellation(); DateTime lastBackupTime = statusManager.GetLastBackupDate(backup.Name);
                string[] allFiles = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);

                // Calculate files that actually need to be copied (only new/modified files)
                var filesToCopy = allFiles.Where(f =>
                    File.GetLastWriteTime(f) > lastBackupTime ||
                    !File.Exists(Path.Combine(backup.TargetDirectory, Path.GetRelativePath(backup.SourceDirectory, f)))
                ).ToList();

                long totalSize = filesToCopy.Sum(f => new FileInfo(f).Length);
                int totalFiles = filesToCopy.Count; // Use actual files to copy count
                List<string> copiedFiles = new();

                // Initialize byte-based progress tracker
                backup.InitializeProgressTracker(totalSize);

                // Register priority files with the manager
                PriorityFileManager.Instance.RegisterPriorityFiles(backup.Name, backup.SourceDirectory);

                // Get priority extensions and separate files (only from files that need copying)
                var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions ?? new List<string>();
                var priorityFiles = filesToCopy
                    .Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();
                var nonPriorityFiles = filesToCopy
                    .Where(f => !priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                // Process priority files first
                foreach (var file in priorityFiles)
                {
                    // Check if paused or cancelled before processing each file
                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    Console.WriteLine($"Fichier détecté : {file}");

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    string? destinationDir = Path.GetDirectoryName(destinationPath); if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }                    // Get current progress percentage from tracker
                    int currentProgress = backup.ProgressTracker?.ProgressPercentage ?? 0;

                    // Créer une entrée de statut ACTIVE
                    var statusEntry = new StatusEntry(
                        backup.Name,
                        file,
                        destinationPath,
                        "ACTIVE",
                        totalFiles,
                        totalSize,
                        totalFiles - copiedFiles.Count,
                        currentProgress,
                        DateTime.Now
                    );
                    statusManager.UpdateStatus(statusEntry);

                    // Check for pause/cancel before file operation
                    cancellationToken.ThrowIfCancellationRequested();
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

                        // Use progress-aware file copy instead of simple File.Copy
                        long bytesCopied = ProgressAwareFileCopy.CopyFileWithProgress(
                            file,
                            destinationPath,
                            (bytes) => backup.ProgressTracker?.AddCopiedBytes(bytes)
                        );

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
                }                // Mark this backup as having processed its priority files
                PriorityFileManager.Instance.MarkBackupAsProcessing(backup.Name);                // Only process non-priority files if allowed
                // Wait until we can process non-priority files
                while (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                {
                    Console.WriteLine($"Sauvegarde '{backup.Name}' en attente : des fichiers prioritaires sont en cours de traitement dans une autre sauvegarde.");
                    backup.State = Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY;

                    // Attendre un délai avant de revérifier
                    Thread.Sleep(1000);
                    Console.WriteLine($"Revérification pour sauvegarde '{backup.Name}'...");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // On peut maintenant traiter les fichiers non-prioritaires
                Console.WriteLine($"Sauvegarde '{backup.Name}' commence/reprend le traitement des fichiers non-prioritaires.");
                backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

                foreach (var file in nonPriorityFiles)
                {
                    // Vérifier s'il y a de nouveaux fichiers prioritaires
                    PriorityFileManager.Instance.CheckAndUpdatePriorityStatus(backup.Name, backup.SourceDirectory);

                    // Si on ne peut plus traiter les fichiers non prioritaires, attendre
                    while (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                    {
                        Console.WriteLine($"Sauvegarde '{backup.Name}' mise en pause : des fichiers prioritaires ont été détectés dans une autre sauvegarde.");
                        backup.State = Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY;

                        // Attendre un délai avant de revérifier
                        Thread.Sleep(1000);
                        Console.WriteLine($"Revérification pour sauvegarde '{backup.Name}'...");
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    // Si on arrive ici, on peut reprendre
                    if (backup.State == Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY)
                    {
                        Console.WriteLine($"Sauvegarde '{backup.Name}' reprend le traitement des fichiers non-prioritaires.");
                        backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;
                    }

                    // Check if paused or cancelled before processing each file
                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    Console.WriteLine($"Fichier détecté : {file}");

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    string? destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }                    // Get current progress percentage from tracker
                    int currentProgress = backup.ProgressTracker?.ProgressPercentage ?? 0;

                    var statusEntry = new StatusEntry(
                        backup.Name,
                        file,
                        destinationPath,
                        "ACTIVE",
                        totalFiles,
                        totalSize,
                        totalFiles - copiedFiles.Count,
                        currentProgress,
                        DateTime.Now
                    );
                    statusManager.UpdateStatus(statusEntry);

                    // Check for pause/cancel before file operation
                    cancellationToken.ThrowIfCancellationRequested();
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

                        // Use progress-aware file copy instead of simple File.Copy
                        long bytesCopied = ProgressAwareFileCopy.CopyFileWithProgress(
                            file,
                            destinationPath,
                            (bytes) => backup.ProgressTracker?.AddCopiedBytes(bytes)
                        );

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
