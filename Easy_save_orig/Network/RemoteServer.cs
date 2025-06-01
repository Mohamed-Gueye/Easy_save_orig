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
    // In: process (BackupProcess)
    // Out: /
    // Description: Initializes the RemoteServer, sets up event listeners for backup state changes, and starts a timer to monitor backup statuses.
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

            backupProcess.BackupManager.BackupPaused += BackupManager_BackupPaused;
            backupProcess.BackupManager.BackupResumed += BackupManager_BackupResumed;

            var timer = new System.Timers.Timer(1000); 
            timer.Elapsed += (sender, e) => CheckBackupStates();
            timer.Start();
        }

        private void BackupManager_BackupPaused(object sender, BackupPausedEventArgs e)
        // In: sender (object), e (BackupPausedEventArgs)
        // Out: void
        // Description: Event handler that broadcasts a pause notification for a backup.
        {
            BroadcastBackupPaused(e.BackupName);
        }

        private void BackupManager_BackupResumed(object sender, BackupResumedEventArgs e)
        // In: sender (object), e (BackupResumedEventArgs)
        // Out: void
        // Description: Event handler that broadcasts a resume notification for a backup.
        {
            BroadcastBackupStarted(e.BackupName);
        }

        private void CheckBackupStates()
        // Out: void
        // Description: Periodically checks the state of all backups and broadcasts any changes to connected clients.
        // Notes: Handles state transitions and updates progress tracking subscriptions.
        {
            var backups = backupProcess.GetAllBackup();

            foreach (var backup in backups)
            {
                if (!lastKnownStates.TryGetValue(backup.Name, out var lastState) || lastState != backup.State)
                {
                    lastKnownStates[backup.Name] = backup.State;

                    switch (backup.State)
                    {
                        case BackupJobState.RUNNING:
                            BroadcastBackupStarted(backup.Name);

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
        // In: port (int)
        // Out: void
        // Description: Starts the TCP server and begins listening for incoming client connections on the specified port.
        {
            if (listener != null) return;

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            cts = new CancellationTokenSource();
            _ = ListenForClientsAsync(cts.Token);
        }

        public void Stop()
        // Out: void
        // Description: Stops the TCP server, cancels listening, and disconnects all active clients.
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
        // In: token (CancellationToken)
        // Out: Task
        // Description: Asynchronously listens for incoming TCP clients and handles them in parallel.
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting clients: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        // In: client (TcpClient), token (CancellationToken)
        // Out: Task
        // Description: Handles communication with a connected client, processes backup commands, and reports state changes.
        // Notes: Uses TCP stream for command/response exchange with basic command parsing.
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            try
            {
                await writer.WriteLineAsync("CONNECTED");

                var backups = backupProcess.GetAllBackup();
                foreach (var backup in backups)
                {
                    await writer.WriteLineAsync($"BACKUP|{backup.Name}|{backup.Type}|{backup.SourceDirectory}|{backup.TargetDirectory}");

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
                    string name = parts.Length > 1 ? parts[1] : "";
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

                                    try
                                    {
                                        await Task.Run(() => backupProcess.RunAllBackups(true));

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
        // In: message (string)
        // Out: void
        // Description: Sends a message to all connected clients. Removes clients that fail during transmission.
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
        // In: backupName (string), percentage (int)
        // Out: void
        // Description: Sends a progress update message for the specified backup to all clients.
        {
            Broadcast($"PROGRESS|{backupName}|{percentage}");
        }

        public void BroadcastBackupState(string backupName, string state)
        // In: backupName (string), state (string)
        // Out: void
        // Description: Sends a state update message for the specified backup to all clients.
        {
            Broadcast($"STATE|{backupName}|{state}");
        }

        public void BroadcastBackupStarted(string backupName)
        // In: backupName (string)
        // Out: void
        // Description: Notifies all clients that the specified backup has started.
        {
            BroadcastBackupState(backupName, "RUNNING");
        }

        public void BroadcastBackupPaused(string backupName)
        // In: backupName (string)
        // Out: void
        // Description: Notifies all clients that the specified backup has been paused.
        {
            BroadcastBackupState(backupName, "PAUSED");
        }

        public void BroadcastBackupStopped(string backupName)
        // In: backupName (string)
        // Out: void
        // Description: Notifies all clients that the specified backup has stopped and resets its progress.
        {
            BroadcastProgress(backupName, 0);
            BroadcastBackupState(backupName, "STOPPED");
        }

        public void BroadcastBackupCompleted(string backupName)
        // In: backupName (string)
        // Out: void
        // Description: Notifies all clients that the specified backup has completed.
        {
            BroadcastBackupState(backupName, "COMPLETED");
        }
    }
}
