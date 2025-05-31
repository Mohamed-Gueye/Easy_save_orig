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
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver, CancellationToken cancellationToken = default)
        // In: backup (Backup), statusManager (StatusManager), logObserver (LogObserver)
        // Out: void
        // Description: Executes a full backup by copying all files from source to destination.
        {
            // Set the backup state to RUNNING at the start
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;
                Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
                Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionManager.EncryptionExecutablePath}");                // Check for cancellation before starting
                backup.CheckPauseAndCancellation(); string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int totalFiles = files.Length;
                int filesDone = 0;

                // Initialize byte-based progress tracker
                backup.InitializeProgressTracker(totalSize);

                // Register priority files with the manager
                PriorityFileManager.Instance.RegisterPriorityFiles(backup.Name, backup.SourceDirectory);

                // Get priority extensions from backup rules
                var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions ?? new List<string>();
                var priorityFiles = files.Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                var nonPriorityFiles = files.Except(priorityFiles).ToList();

                void ProcessFile(string file)
                {
                    // Check if cancelled via external CancellationToken
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check if paused or cancelled before processing each file
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
                        totalFiles - filesDone,
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
                    }
                    finally
                    {
                        if (isLargeFile)
                        {
                            LargeFileTransferManager.Instance.ReleaseLargeFileTransfer();
                        }
                    }

                    // Check for pause/cancel after file copy
                    backup.CheckPauseAndCancellation();

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
                            return; // Exit the ProcessFile function instead of continue
                        }
                    }
                    logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
                    filesDone++;
                }

                // Process priority files first
                foreach (var file in priorityFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProcessFile(file);
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
                    cancellationToken.ThrowIfCancellationRequested();
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
                    ProcessFile(file);
                }

                // Set final state to COMPLETED if we get here without cancellation
                backup.State = Easy_Save.Model.Enum.BackupJobState.COMPLETED;
                backup.Progress = "100%";

                // Clean up priority file tracking
                PriorityFileManager.Instance.RemoveBackup(backup.Name);

                // Mise à jour du statut dans le fichier state.json
                var finalStatus = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == backup.Name);
                if (finalStatus != null)
                {
                    finalStatus.State = "COMPLETED";
                    finalStatus.Progression = 100;
                    finalStatus.NbFilesLeftToDo = 0;
                    statusManager.UpdateStatus(finalStatus);
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                Console.WriteLine($"Backup job '{backup.Name}' was stopped by the user.");
                backup.State = Easy_Save.Model.Enum.BackupJobState.STOPPED;

                // Mise à jour du statut dans le fichier state.json pour l'annulation
                var cancelStatus = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == backup.Name);
                if (cancelStatus != null)
                {
                    cancelStatus.State = "STOPPED";
                    statusManager.UpdateStatus(cancelStatus);
                }
            }
            catch (Exception ex)
            {
                // Handle other errors
                Console.WriteLine($"Error during backup: {ex.Message}");
                backup.State = Easy_Save.Model.Enum.BackupJobState.ERROR;

                // Mise à jour du statut dans le fichier state.json pour l'erreur
                var errorStatus = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == backup.Name);
                if (errorStatus != null)
                {
                    errorStatus.State = "ERROR";
                    statusManager.UpdateStatus(errorStatus);
                }

                throw;
            }
        }
    }
}
