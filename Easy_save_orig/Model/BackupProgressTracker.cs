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
        {
            this.backupProcess = backupProcess ?? throw new ArgumentNullException(nameof(backupProcess));
            this.progressTimer = new System.Timers.Timer(500); // Vérifier toutes les 500ms
            this.progressTimer.AutoReset = true;
        }

        public async Task ExecuteBackupWithProgressAsync(string backupName, IProgress<(int Current, int Total)> progress, CancellationToken cancellationToken)
        {
            // Start progress monitoring
            StatusEntry currentStatus = null;
            
            // Configurer le timer pour suivre la progression
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
                // Démarrer le timer de progression
                progressTimer.Start();
                
                // Exécuter la sauvegarde dans un thread séparé
                await Task.Run(() => backupProcess.ExecuteBackup(backupName), cancellationToken);
                
                // Faire une dernière vérification pour s'assurer que nous rapportons l'état final
                currentStatus = GetBackupStatus(backupName);
                if (currentStatus != null)
                {
                    progress.Report((currentStatus.Progression, 100));
                }
            }
            finally
            {
                // Arrêter le timer de progression
                progressTimer.Stop();
            }
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
                    // Configurer le timer pour ce backup spécifique
                    string currentBackupName = backup.Name;
                    StatusEntry currentStatus = null;
                    
                    progressTimer.Elapsed += null; // Retirer tous les gestionnaires d'événements précédents
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
                    
                    // Démarrer le suivi de progression
                    progressTimer.Start();
                    
                    // Rapport initial
                    progress.Report((backup.Name, 0, 100));
                    
                    // Exécuter la sauvegarde en arrière-plan
                    await Task.Run(() => backupProcess.ExecuteBackup(backup.Name), cancellationToken);
                    
                    // Vérifier l'état final
                    currentStatus = GetBackupStatus(backup.Name);
                    if (currentStatus != null)
                    {
                        progress.Report((backup.Name, currentStatus.Progression, 100));
                    }
                    
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    // Propager l'annulation
                    throw;
                }
                catch (Exception)
                {
                    // Continuer avec les autres sauvegardes en cas d'erreur
                }
                finally
                {
                    // Arrêter le timer pour ce backup
                    progressTimer.Stop();
                }
            }
            
            return successCount;
        }
        
        private StatusEntry GetBackupStatus(string backupName)
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    string json = File.ReadAllText(stateFilePath);
                    List<StatusEntry> statusEntries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                    
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