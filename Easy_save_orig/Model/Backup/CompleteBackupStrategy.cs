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
        {
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;
                var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions.Select(ext => ext.ToLower()).ToList();

                Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
                Console.WriteLine($"[DEBUG] Extensions prioritaires : {string.Join(", ", priorityExtensions)}");

                backup.CheckPauseAndCancellation();

                string[] allFiles = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
                long totalSize = allFiles.Sum(f => new FileInfo(f).Length);
                int totalFiles = allFiles.Length;
                int filesDone = 0;

                // Première passe : fichiers prioritaires
                var priorityFiles = allFiles.Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                var nonPriorityFiles = allFiles.Except(priorityFiles).ToList();

                void ProcessFile(string file)
                {
                    backup.CheckPauseAndCancellation();

                    Console.WriteLine($"Fichier détecté : {file}");

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    string? destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir!);
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

                    backup.Progress = $"{(int)((filesDone / (double)totalFiles) * 100)}%";

                    Thread.Sleep(2000);

                    backup.CheckPauseAndCancellation();

                    bool shouldEncrypt = encryptionManager.ShouldEncryptFile(file);

                    int encryptionTime = 0;
                    int transferTime = 0;

                    var sw = Stopwatch.StartNew();
                    File.Copy(file, destinationPath, true);
                    sw.Stop();
                    transferTime = (int)sw.Elapsed.TotalMilliseconds;

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
                            return;
                        }
                    }

                    logObserver.Update(backup, totalSize, transferTime, shouldEncrypt ? encryptionTime : 0, totalFiles);
                    filesDone++;
                }

                // Traitement des fichiers prioritaires
                foreach (var file in priorityFiles)
                {
                    ProcessFile(file);
                }

                // Traitement des fichiers non-prioritaires
                foreach (var file in nonPriorityFiles)
                {
                    ProcessFile(file);
                }

                backup.State = Easy_Save.Model.Enum.BackupJobState.COMPLETED;
                backup.Progress = "100%";

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
