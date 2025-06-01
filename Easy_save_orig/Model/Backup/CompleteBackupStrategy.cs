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
        // In: backup (Backup), statusManager (StatusManager), logObserver (LogObserver), cancellationToken (optional)
        // Out: void
        // Description: Performs an incremental backup by copying only new or modified files, while handling encryption, priority file access coordination, cancellation, pause/resume, and progress tracking.
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver, CancellationToken cancellationToken = default)
        {
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;

                // Get all source files, but only copy the ones that are new or modified since the last backup
                cancellationToken.ThrowIfCancellationRequested();
                backup.CheckPauseAndCancellation();

                DateTime lastBackupTime = statusManager.GetLastBackupDate(backup.Name);
                string[] allFiles = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);

                var filesToCopy = allFiles.Where(f =>
                    File.GetLastWriteTime(f) > lastBackupTime ||
                    !File.Exists(Path.Combine(backup.TargetDirectory, Path.GetRelativePath(backup.SourceDirectory, f)))
                ).ToList();

                long totalSize = filesToCopy.Sum(f => new FileInfo(f).Length);
                int totalFiles = filesToCopy.Count;
                List<string> copiedFiles = new();

                backup.InitializeProgressTracker(totalSize);

                // Priority file management (synchronization mechanism between backups)
                PriorityFileManager.Instance.RegisterPriorityFiles(backup.Name, backup.SourceDirectory);

                var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions ?? new List<string>();
                var priorityFiles = filesToCopy
                    .Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();
                var nonPriorityFiles = filesToCopy
                    .Where(f => !priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                // First process priority files (mutex-like coordination is managed by PriorityFileManager)
                foreach (var file in priorityFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

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

                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);
                    int encryptionTime = 0;
                    int transferTime = 0;

                    // This is used to limit how many large files are transferred in parallel
                    bool isLargeFile = LargeFileTransferManager.Instance.IsFileLarge(file, BackupRulesManager.Instance.LargeFileSizeThresholdKB);

                    if (isLargeFile)
                        LargeFileTransferManager.Instance.WaitForLargeFileTransferAsync().Wait(); // Acts like a semaphore

                    try
                    {
                        var sw = Stopwatch.StartNew();

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
                            LargeFileTransferManager.Instance.ReleaseLargeFileTransfer();
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

                            if (decryptTime < 0)
                                Console.WriteLine($"Decryption failed for: {decryptedPath}");
                        }
                        else
                        {
                            Console.WriteLine($"Encryption failed: {destinationPath} (code {encryptionTime})");
                            continue;
                        }
                    }

                    logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
                }

                PriorityFileManager.Instance.MarkBackupAsProcessing(backup.Name);

                // We wait until it’s safe to process non-priority files
                while (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                {
                    backup.State = Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY;
                    Thread.Sleep(1000);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

                foreach (var file in nonPriorityFiles)
                {
                    // If new priority files are detected elsewhere, we pause again
                    PriorityFileManager.Instance.CheckAndUpdatePriorityStatus(backup.Name, backup.SourceDirectory);

                    while (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                    {
                        backup.State = Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY;
                        Thread.Sleep(1000);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (backup.State == Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY)
                        backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

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

                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);
                    int encryptionTime = 0;
                    int transferTime = 0;

                    bool isLargeFile = LargeFileTransferManager.Instance.IsFileLarge(file, BackupRulesManager.Instance.LargeFileSizeThresholdKB);

                    if (isLargeFile)
                        LargeFileTransferManager.Instance.WaitForLargeFileTransferAsync().Wait();

                    try
                    {
                        var sw = Stopwatch.StartNew();

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
                            LargeFileTransferManager.Instance.ReleaseLargeFileTransfer();
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

                            if (decryptTime < 0)
                                Console.WriteLine($"Decryption failed for: {decryptedPath}");
                        }
                        else
                        {
                            Console.WriteLine($"Encryption failed: {destinationPath} (code {encryptionTime})");
                            continue;
                        }
                    }

                    logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
                }

                PriorityFileManager.Instance.RemoveBackup(backup.Name);

                backup.State = Easy_Save.Model.Enum.BackupJobState.COMPLETED;
                backup.Progress = "100%";
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Backup job '{backup.Name}' was stopped by the user.");
                backup.State = Easy_Save.Model.Enum.BackupJobState.STOPPED;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup: {ex.Message}");
                backup.State = Easy_Save.Model.Enum.BackupJobState.ERROR;
                throw;
            }
        }
    }
}
