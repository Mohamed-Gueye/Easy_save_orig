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
        // In: 
        //   - backup (Backup): the backup configuration and progress
        //   - statusManager (StatusManager): updates the job state and statistics
        //   - logObserver (LogObserver): logs performance data (time, size, etc.)
        //   - cancellationToken (optional): allows the operation to be cancelled mid-run
        // Out: void
        // Description: Performs a full backup by copying every file from the source directory, regardless of whether it changed or not. Also handles encryption, concurrency, pause/resume support, and respects global file copy coordination for large and priority files.
        public void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver, CancellationToken cancellationToken = default)
        {
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;

                backup.CheckPauseAndCancellation();

                string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int totalFiles = files.Length;
                int filesDone = 0;

                // Byte-based progress tracking (more accurate for large files)
                backup.InitializeProgressTracker(totalSize);

                // Priority file system: acts like a scheduling/mutex mechanism between jobs
                PriorityFileManager.Instance.RegisterPriorityFiles(backup.Name, backup.SourceDirectory);

                var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions ?? new List<string>();
                var priorityFiles = files.Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                var nonPriorityFiles = files.Except(priorityFiles).ToList();

                void ProcessFile(string file)
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
                        totalFiles - filesDone,
                        currentProgress,
                        DateTime.Now
                    );
                    statusManager.UpdateStatus(statusEntry);

                    cancellationToken.ThrowIfCancellationRequested();
                    backup.CheckPauseAndCancellation();

                    bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);
                    int encryptionTime = 0;
                    int transferTime = 0;

                    // This part acts like a semaphore: only a certain number of large file transfers can happen simultaneously
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
                    }
                    finally
                    {
                        if (isLargeFile)
                            LargeFileTransferManager.Instance.ReleaseLargeFileTransfer();
                    }

                    backup.CheckPauseAndCancellation();

                    if (shouldEncrypt)
                    {
                        encryptionTime = encryptionManager.EncryptFile(destinationPath);

                        if (encryptionTime >= 0)
                        {
                            // Decrypt copy is made just for testing or verification
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
                            Console.WriteLine($"Encryption failed for: {destinationPath} (code {encryptionTime})");
                            return;
                        }
                    }

                    logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
                    filesDone++;
                }

                // Handle priority files first (if multiple backups are running, this avoids collisions)
                foreach (var file in priorityFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProcessFile(file);
                }

                PriorityFileManager.Instance.MarkBackupAsProcessing(backup.Name);

                // Wait for permission to process non-priority files
                while (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                {
                    backup.State = Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY;
                    Thread.Sleep(1000);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

                foreach (var file in nonPriorityFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check again for priority file conflicts (other jobs might’ve started)
                    PriorityFileManager.Instance.CheckAndUpdatePriorityStatus(backup.Name, backup.SourceDirectory);

                    while (!PriorityFileManager.Instance.CanProcessNonPriorityFiles(backup.Name))
                    {
                        backup.State = Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY;
                        Thread.Sleep(1000);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (backup.State == Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY)
                    {
                        backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;
                    }

                    ProcessFile(file);
                }

                // Everything went fine → finish and clean up
                backup.State = Easy_Save.Model.Enum.BackupJobState.COMPLETED;
                backup.Progress = "100%";

                PriorityFileManager.Instance.RemoveBackup(backup.Name);

                // Final status update
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
                Console.WriteLine($"Backup job '{backup.Name}' was stopped by the user.");
                backup.State = Easy_Save.Model.Enum.BackupJobState.STOPPED;

                var cancelStatus = statusManager.GetAllStatuses().FirstOrDefault(s => s.Name == backup.Name);
                if (cancelStatus != null)
                {
                    cancelStatus.State = "STOPPED";
                    statusManager.UpdateStatus(cancelStatus);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup: {ex.Message}");
                backup.State = Easy_Save.Model.Enum.BackupJobState.ERROR;

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
