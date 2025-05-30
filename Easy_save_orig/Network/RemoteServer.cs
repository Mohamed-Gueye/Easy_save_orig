using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Easy_Save.Controller;
using Easy_Save.Model;
using System.Linq;
using Easy_Save.Model.Enum;

namespace Easy_Save.Network
{
    public class RemoteServer
    {
        private TcpListener listener;
        private readonly List<TcpClient> clients = new();
        private readonly BackupProcess backupProcess;
        private CancellationTokenSource cts;
        private readonly Dictionary<string, Action<int>> progressHandlers = new();
        private readonly Dictionary<string, BackupJobState> lastKnownStates = new();

        public bool IsRunning => listener != null;

        public RemoteServer(BackupProcess process)
        {
            backupProcess = process;

            // S'abonner aux événements du BackupManager pour être notifié des changements
            backupProcess.BackupManager.BackupPaused += BackupManager_BackupPaused;
            backupProcess.BackupManager.BackupResumed += BackupManager_BackupResumed;

            // Démarrer un timer pour vérifier régulièrement l'état des sauvegardes
            var timer = new System.Timers.Timer(1000); // vérifier toutes les secondes
            timer.Elapsed += (sender, e) => CheckBackupStates();
            timer.Start();
        }

        private void BackupManager_BackupPaused(object sender, BackupPausedEventArgs e)
        {
            // Notifier les clients que la sauvegarde a été mise en pause
            BroadcastBackupPaused(e.BackupName);
        }

        private void BackupManager_BackupResumed(object sender, BackupResumedEventArgs e)
        {
            // Notifier les clients que la sauvegarde a repris
            BroadcastBackupStarted(e.BackupName);
        }

        private void CheckBackupStates()
        {
            var backups = backupProcess.GetAllBackup();

            foreach (var backup in backups)
            {
                // Si nous n'avons pas encore enregistré cet état ou s'il a changé
                if (!lastKnownStates.TryGetValue(backup.Name, out var lastState) || lastState != backup.State)
                {
                    // Mettre à jour l'état connu
                    lastKnownStates[backup.Name] = backup.State;

                    // Notifier les clients du changement
                    switch (backup.State)
                    {
                        case BackupJobState.RUNNING:
                            BroadcastBackupStarted(backup.Name);

                            // S'abonner à l'événement de progression si ce n'est pas déjà fait
                            if (!progressHandlers.ContainsKey(backup.Name) && backup.ProgressTracker != null)
                            {
                                Action<int> progressHandler = (percentage) =>
                                {
                                    BroadcastProgress(backup.Name, percentage);
                                };
                                backup.ProgressTracker.ProgressChanged += progressHandler;
                                progressHandlers[backup.Name] = progressHandler;
                            }
                            break;

                        case BackupJobState.PAUSED:
                        case BackupJobState.PAUSED_FOR_PRIORITY:
                            BroadcastBackupPaused(backup.Name);
                            break;

                        case BackupJobState.STOPPED:
                            BroadcastBackupStopped(backup.Name);

                            // Se désabonner de l'événement de progression
                            if (progressHandlers.TryGetValue(backup.Name, out var handler))
                            {
                                if (backup.ProgressTracker != null)
                                {
                                    backup.ProgressTracker.ProgressChanged -= handler;
                                }
                                progressHandlers.Remove(backup.Name);
                            }
                            break;

                        case BackupJobState.COMPLETED:
                            BroadcastBackupCompleted(backup.Name);

                            // Se désabonner de l'événement de progression
                            if (progressHandlers.TryGetValue(backup.Name, out var completedHandler))
                            {
                                if (backup.ProgressTracker != null)
                                {
                                    backup.ProgressTracker.ProgressChanged -= completedHandler;
                                }
                                progressHandlers.Remove(backup.Name);
                            }
                            break;
                    }
                }

                // Vérifier si on doit s'abonner à la progression
                if (backup.State == BackupJobState.RUNNING &&
                    !progressHandlers.ContainsKey(backup.Name) &&
                    backup.ProgressTracker != null)
                {
                    Action<int> progressHandler = (percentage) =>
                    {
                        BroadcastProgress(backup.Name, percentage);
                    };
                    backup.ProgressTracker.ProgressChanged += progressHandler;
                    progressHandlers[backup.Name] = progressHandler;
                }
            }
        }

        public void Start(int port = 9000)
        {
            if (listener != null) return;

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            cts = new CancellationTokenSource();
            _ = ListenForClientsAsync(cts.Token);
        }

        public void Stop()
        {
            try
            {
                cts?.Cancel();
                listener?.Stop();
                listener = null;

                lock (clients)
                {
                    foreach (var client in clients)
                        client.Close();
                    clients.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while stopping server: {ex.Message}");
            }
        }

        private async Task ListenForClientsAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(token);
                    lock (clients)
                    {
                        clients.Add(client);
                    }
                    _ = HandleClientAsync(client, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Server stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting clients: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            try
            {
                await writer.WriteLineAsync("CONNECTED");

                // Send initial list of backups
                var backups = backupProcess.GetAllBackup();
                foreach (var backup in backups)
                {
                    await writer.WriteLineAsync($"BACKUP|{backup.Name}|{backup.Type}|{backup.SourceDirectory}|{backup.TargetDirectory}");

                    // Envoyer également l'état actuel des sauvegardes existantes
                    string state = "READY";
                    if (backup.State == BackupJobState.RUNNING)
                        state = "RUNNING";
                    else if (backup.State == BackupJobState.PAUSED ||
                             backup.State == BackupJobState.PAUSED_FOR_PRIORITY)
                        state = "PAUSED";
                    else if (backup.State == BackupJobState.COMPLETED)
                        state = "COMPLETED";
                    else if (backup.State == BackupJobState.STOPPED)
                        state = "STOPPED";

                    await writer.WriteLineAsync($"STATE|{backup.Name}|{state}");
                }

                while (!token.IsCancellationRequested && client.Connected)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) break;

                    var parts = line.Split('|');
                    if (parts.Length < 1) continue;

                    string cmd = parts[0];
                    string name = parts.Length > 1 ? parts[1] : "";// Traiter les commandes de manière asynchrone pour ne pas bloquer la boucle de lecture
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            switch (cmd)
                            {
                                case "START_ALL":
                                    Console.WriteLine("Client requested to start all backups");
                                    // Notifier les clients que chaque sauvegarde a démarré
                                    var allBackups = backupProcess.GetAllBackup();
                                    foreach (var backup in allBackups)
                                    {
                                        BroadcastBackupStarted(backup.Name);
                                    }

                                    // Exécuter toutes les sauvegardes en parallèle, tout en respectant les contraintes
                                    try
                                    {
                                        // Utilise true pour l'exécution concurrente mais limitée
                                        // Cela préservera les fonctionnalités de priorité de fichiers,
                                        // de limitation pour les gros fichiers et de blocage lors de l'exécution 
                                        // des logiciels métier (business software)
                                        await Task.Run(() => backupProcess.RunAllBackups(true));

                                        // Aucun besoin de gérer les sauvegardes individuellement
                                        // car le backupProcess.RunAllBackups va s'assurer que chaque sauvegarde
                                        // respecte les règles définies et met à jour son état correctement
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error executing backups: {ex.Message}");
                                    }
                                    break;
                                case "START":
                                    Console.WriteLine($"Client requested to start backup: {name}");
                                    BroadcastBackupStarted(name);
                                    await Task.Run(() => backupProcess.ExecuteBackup(name));
                                    // Vérifier l'état final après l'exécution
                                    var statusManager = new Easy_Save.Model.IO.StatusManager();
                                    var status = statusManager.GetStatusByName(name);
                                    if (status != null && status.State == "COMPLETED")
                                    {
                                        BroadcastBackupCompleted(name);
                                    }
                                    break;
                                case "PAUSE":
                                    Console.WriteLine($"Client requested to pause backup: {name}");
                                    backupProcess.BackupManager.PauseBackup(name);
                                    BroadcastBackupPaused(name);
                                    break;
                                case "RESUME":
                                    Console.WriteLine($"Client requested to resume backup: {name}");
                                    backupProcess.BackupManager.ResumeBackup(name);
                                    BroadcastBackupStarted(name);
                                    break;
                                case "STOP":
                                    Console.WriteLine($"Client requested to stop backup: {name}");
                                    backupProcess.StopBackup(name);
                                    // Forcer la remise à zéro de la progression
                                    BroadcastProgress(name, 0);
                                    BroadcastBackupStopped(name);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing command {cmd} for {name}: {ex.Message}");
                            BroadcastBackupStopped(name);
                        }
                    }, token);
                }
            }
            catch (IOException) { /* Client disconnected */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                lock (clients) clients.Remove(client);
                client.Close();
            }
        }

        public void Broadcast(string message)
        {
            lock (clients)
            {
                foreach (var client in clients.ToList())
                {
                    try
                    {
                        var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                        writer.WriteLine(message);
                    }
                    catch
                    {
                        client.Close();
                        clients.Remove(client);
                    }
                }
            }
        }

        public void BroadcastProgress(string backupName, int percentage)
        {
            Broadcast($"PROGRESS|{backupName}|{percentage}");
        }

        public void BroadcastBackupState(string backupName, string state)
        {
            Broadcast($"STATE|{backupName}|{state}");
        }

        public void BroadcastBackupStarted(string backupName)
        {
            BroadcastBackupState(backupName, "RUNNING");
        }

        public void BroadcastBackupPaused(string backupName)
        {
            BroadcastBackupState(backupName, "PAUSED");
        }

        public void BroadcastBackupStopped(string backupName)
        {
            // Lorsqu'une sauvegarde est arrêtée, on réinitialise sa progression à 0
            BroadcastProgress(backupName, 0);
            BroadcastBackupState(backupName, "STOPPED");
        }

        public void BroadcastBackupCompleted(string backupName)
        {
            BroadcastBackupState(backupName, "COMPLETED");
        }
    }
}
