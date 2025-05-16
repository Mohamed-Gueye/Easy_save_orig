using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Easy_Save.Controller;

namespace Easy_Save.Model
{
    public class BackupProgressTracker
    {
        private readonly BackupProcess backupProcess;

        public BackupProgressTracker(BackupProcess backupProcess)
        {
            this.backupProcess = backupProcess ?? throw new ArgumentNullException(nameof(backupProcess));
        }

        public async Task ExecuteBackupWithProgressAsync(string backupName, IProgress<(int Current, int Total)> progress, CancellationToken cancellationToken)
        {
            // This is a simulated progression since we don't have direct access to the file copy details
            // In a real implementation, you would modify the BackupManager and strategies to report progress
            
            // Simulate starting the backup
            await Task.Delay(500, cancellationToken);
            
            // Run the backup in a background task
            await Task.Run(() => 
            {
                // Start the actual backup process
                backupProcess.ExecuteBackup(backupName);
                
                // For demo purposes, simulate progress
                SimulateProgressReporting(progress, cancellationToken);
                
            }, cancellationToken);
        }
        
        public async Task<int> ExecuteAllBackupsWithProgressAsync(IProgress<(string BackupName, int Current, int Total)> progress, CancellationToken cancellationToken)
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
                    // Report which backup we're starting
                    progress.Report((backup.Name, 0, 100));
                    
                    // Execute the backup
                    await Task.Run(() => 
                    {
                        backupProcess.ExecuteBackup(backup.Name);
                        
                        // Simulate progress for this specific backup
                        SimulateProgressReporting(new Progress<(int Current, int Total)>(p => 
                            progress.Report((backup.Name, p.Current, p.Total))), 
                            cancellationToken);
                        
                    }, cancellationToken);
                    
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    // Propagate cancellation
                    throw;
                }
                catch (Exception)
                {
                    // Log the error but continue with other backups
                    // In a real implementation, you'd want to log this
                }
            }
            
            return successCount;
        }
        
        private void SimulateProgressReporting(IProgress<(int Current, int Total)> progress, CancellationToken cancellationToken)
        {
            // This is a simulation for demonstration purposes
            // In a real implementation, this would be based on actual file counting and copying progress
            
            const int totalFiles = 100; // Simulated total
            
            for (int i = 1; i <= totalFiles; i++)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                
                // Report progress
                progress.Report((i, totalFiles));
                
                // Small delay to simulate work
                try
                {
                    Thread.Sleep(50); // 50ms per "file"
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
            }
        }
    }
} 