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
            backup.State = Easy_Save.Model.Enum.BackupJobState.RUNNING;

            try
            {
                var encryptionManager = EncryptionManager.Instance;
                Console.WriteLine($"[DEBUG] Extensions à chiffrer chargées : {string.Join(", ", encryptionManager.ExtensionsToEncrypt)}");
                Console.WriteLine($"[DEBUG] Chemin CryptoSoft.exe : {encryptionManager.EncryptionExecutablePath}");

                backup.CheckPauseAndCancellation();

                string[] files = Directory.GetFiles(backup.SourceDirectory, "*", SearchOption.AllDirectories);
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int totalFiles = files.Length;
                List<string> copiedFiles = new();

                foreach (string file in files)
                {
                    backup.CheckPauseAndCancellation();

                    string relativePath = Path.GetRelativePath(backup.SourceDirectory, file);
                    string destinationPath = Path.Combine(backup.TargetDirectory, relativePath);
                    string? destinationDir = Path.GetDirectoryName(destinationPath);

                    bool destinationFileExists = File.Exists(destinationPath);
                    bool fileModified = destinationFileExists &&
                        File.GetLastWriteTime(file) > File.GetLastWriteTime(destinationPath);

                    if (!destinationFileExists || fileModified)
                    {
                        if (!Directory.Exists(destinationDir))
                            Directory.CreateDirectory(destinationDir);

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

                        var swa = Stopwatch.StartNew();
                        File.Copy(file, destinationPath, true);
                        swa.Stop();
                        transferTime = (int)swa.Elapsed.TotalMilliseconds;

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
                        copiedFiles.Add(file);
                    }
                }

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
