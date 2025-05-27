using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Linq;
using Easy_Save.Controller;
using Easy_Save.Model.Status;
using System.Timers;

namespace Easy_Save.Model
{
    public class BackupProgressTracker
    {
        private readonly BackupProcess backupProcess;
        private System.Timers.Timer progressTimer;
        private string stateFilePath = "state.json";

        public BackupProgressTracker(BackupProcess backupProcess)
        // In: backupProcess (BackupProcess)
        // Out: /
        // Description: Initializes the progress tracker with the backup process and sets the timer.
        {
            this.backupProcess = backupProcess ?? throw new ArgumentNullException(nameof(backupProcess));
            this.progressTimer = new System.Timers.Timer(500);
            this.progressTimer.AutoReset = true;
        }

        public async Task ExecuteBackupWithProgressAsync(string backupName, IProgress<(int Current, int Total)> progress, CancellationToken cancellationToken)
        // In: backupName (string), progress (IProgress), cancellationToken (CancellationToken)
        // Out: Task
        // Description: Executes a backup while reporting progress asynchronously.
        {
            StatusEntry? currentStatus = null;
            
            progressTimer.Elapsed += (sender, e) => 
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    currentStatus = GetBackupStatus(backupName);
                    if (currentStatus != null)
                    {
                        progress.Report((currentStatus.Progression, 100));
                    }
                }
            };
            
            try
            {
                progressTimer.Start();
                
                await Task.Run(() => backupProcess.ExecuteBackup(backupName), cancellationToken);
                
                currentStatus = GetBackupStatus(backupName);
                if (currentStatus != null)
                {
                    progress.Report((currentStatus.Progression, 100));
                }
            }
            finally
            {
                progressTimer.Stop();
            }
        }
        
        public async Task<int> ExecuteAllBackupsWithProgressAsync(IProgress<(string BackupName, int Current, int Total)> progress, CancellationToken cancellationToken)
        // In: progress (IProgress), cancellationToken (CancellationToken)
        // Out: Task<int>
        // Description: Executes all backups while tracking and reporting progress.
        {
            var backups = backupProcess.GetAllBackup();
            int successCount = 0;
            
            foreach (var backup in backups)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                
                try
                {
                    string currentBackupName = backup.Name;
                    StatusEntry? currentStatus = null;
                    
                    progressTimer.Elapsed += null; 
                    progressTimer.Elapsed += (sender, e) => 
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            currentStatus = GetBackupStatus(currentBackupName);
                            if (currentStatus != null)
                            {
                                progress.Report((currentBackupName, currentStatus.Progression, 100));
                            }
                        }
                    };
                    
                    progressTimer.Start();
                    
                    progress.Report((backup.Name, 0, 100));
                    
                    await Task.Run(() => backupProcess.ExecuteBackup(backup.Name), cancellationToken);
                    
                    currentStatus = GetBackupStatus(backup.Name);
                    if (currentStatus != null)
                    {
                        progress.Report((backup.Name, currentStatus.Progression, 100));
                    }
                    
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                }
                finally
                {
                    progressTimer.Stop();
                }
            }
            
            return successCount;
        }
        
        private StatusEntry? GetBackupStatus(string backupName)
        // In: backupName (string)
        // Out: StatusEntry 
        // Description: Retrieves the backup status from the state file by name.
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    string json = File.ReadAllText(stateFilePath);
                    List<StatusEntry>? statusEntries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                    
                    if (statusEntries != null)
                    {
                        return statusEntries.FirstOrDefault(e => e.Name == backupName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la lecture du fichier d'état: {ex.Message}");
            }
            
            return null;
        }
    }
} 