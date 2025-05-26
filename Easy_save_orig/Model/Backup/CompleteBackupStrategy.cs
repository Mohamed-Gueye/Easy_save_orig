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
            // Set the backup state to RUNNING at the start
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;
                Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
                Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionManager.EncryptionExecutablePath}");

                // Check for cancellation before starting
                backup.CheckPauseAndCancellation();

                string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int totalFiles = files.Length;
                int filesDone = 0;

<<<<<<< HEAD
                foreach (string file in files)
=======
                var priorityFiles = allFiles.Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                var nonPriorityFiles = allFiles.Except(priorityFiles).ToList();

                void ProcessFile(string file)
>>>>>>> 7cef8fd (Add of the large file manager)
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

                    // Update backup progress
                    backup.Progress = $"{(int)((filesDone / (double)totalFiles) * 100)}%";

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
                            continue;
                        }
                    }

                    logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
                    filesDone++;
<<<<<<< HEAD
                }                // Set final state to COMPLETED if we get here without cancellation
=======
                }

                foreach (var file in priorityFiles)
                {
                    ProcessFile(file);
                }

                foreach (var file in nonPriorityFiles)
                {
                    ProcessFile(file);
                }

>>>>>>> 7cef8fd (Add of the large file manager)
                backup.State = Easy_Save.Model.Enum.BackupJobState.COMPLETED;
                backup.Progress = "100%";

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
