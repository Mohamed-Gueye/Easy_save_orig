using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.ComponentModel;

namespace Easy_save_client
{
    public partial class MainWindow : Window
    {
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;
        private CancellationTokenSource? cts;

        private ObservableCollection<BackupInfo> backups = new();

        public MainWindow()
        {
            InitializeComponent();
            lstBackups.ItemsSource = backups;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            // Si déjà connecté, déconnecter
            if (client != null && client.Connected)
            {
                DisconnectFromServer();
                btnConnect.Content = "Connect";
                return;
            }

            try
            {
                client = new TcpClient();
                await client.ConnectAsync(txtHost.Text, 9000);

                var stream = client.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream) { AutoFlush = true };

                MessageBox.Show("Connected to server.");
                btnConnect.Content = "Disconnect";
                cts = new CancellationTokenSource();
                _ = ListenForUpdatesAsync(cts.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection failed: " + ex.Message);
            }
        }

        private async Task ListenForUpdatesAsync(CancellationToken token)
        {
            try
            {
                bool isFirstBackupReceived = true;

                while (!token.IsCancellationRequested && client != null && client.Connected && reader != null)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    Dispatcher.Invoke(() =>
                    {
                        if (line.StartsWith("BACKUP|"))
                        {
                            if (isFirstBackupReceived)
                            {
                                backups.Clear();
                                isFirstBackupReceived = false;
                            }

                            var parts = line.Split('|');
                            if (parts.Length == 5)
                            {
                                string name = parts[1];
                                if (!backups.Any(b => b.Name == name))
                                {
                                    backups.Add(new BackupInfo
                                    {
                                        Name = name,
                                        Type = parts[2],
                                        Source = parts[3],
                                        Target = parts[4],
                                        Progress = 0,
                                        State = "READY"
                                    });
                                }
                            }
                        }
                        else if (line.StartsWith("PROGRESS|"))
                        {
                            var parts = line.Split('|');
                            if (parts.Length == 3)
                            {
                                string name = parts[1];
                                if (int.TryParse(parts[2], out int percent))
                                {
                                    var backup = backups.FirstOrDefault(b => b.Name == name);
                                    if (backup != null)
                                    {
                                        backup.Progress = percent;
                                    }
                                }
                            }
                        }
                        else if (line.StartsWith("STATE|"))
                        {
                            var parts = line.Split('|');
                            if (parts.Length == 3)
                            {
                                string name = parts[1];
                                string state = parts[2];

                                var backup = backups.FirstOrDefault(b => b.Name == name);
                                if (backup != null)
                                {
                                    backup.State = state;

                                    // Si la sauvegarde est terminée ou arrêtée, mettre à jour la progression
                                    if (state == "COMPLETED")
                                    {
                                        backup.Progress = 100;
                                    }
                                    else if (state == "STOPPED")
                                    {
                                        // Réinitialiser la progression si la sauvegarde est arrêtée
                                        backup.Progress = 0;
                                    }
                                }
                            }
                        }
                        else if (line.StartsWith("DELETED|"))
                        {
                            var parts = line.Split('|');
                            if (parts.Length == 2)
                            {
                                string name = parts[1];
                                var backup = backups.FirstOrDefault(b => b.Name == name);
                                if (backup != null)
                                {
                                    backups.Remove(backup);
                                }
                            }
                        }
                    });
                }
            }
            catch (IOException) { }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Error: " + ex.Message));
            }
        }
        private async void SendCommand(string command, string? backupName = null)
        {
            if (writer == null || client == null || !client.Connected) return;

            try
            {
                string message;
                if (backupName != null)
                {
                    message = $"{command}|{backupName}";
                    Console.WriteLine($"Sending command {command} for backup {backupName}");
                }
                else
                {
                    message = command;
                    Console.WriteLine($"Sending global command {command}");
                }

                await writer.WriteLineAsync(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to send command: " + ex.Message);
            }
        }

        private async void SendCommandForBackup(string command, BackupInfo backup)
        {
            if (writer == null || client == null || !client.Connected) return;

            try
            {
                Console.WriteLine($"Sending command {command} for backup {backup.Name}");
                string message = $"{command}|{backup.Name}";
                await writer.WriteLineAsync(message);

                // Mettre à jour visuellement l'état en attendant la confirmation du serveur
                switch (command)
                {
                    case "START":
                        backup.State = "PENDING...";
                        break;
                    case "PAUSE":
                        backup.State = "PENDING PAUSE...";
                        break;
                    case "RESUME":
                        backup.State = "RESUMING...";
                        break;
                    case "STOP":
                        backup.State = "PENDING STOP...";
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to send command: " + ex.Message);
            }
        }
        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not BackupInfo backup) return;

            switch (backup.State)
            {
                case "READY":
                case "STOPPED":
                case "COMPLETED":
                    SendCommandForBackup("START", backup);
                    break;
                case "RUNNING":
                    SendCommandForBackup("PAUSE", backup);
                    break;
                case "PAUSED":
                    SendCommandForBackup("RESUME", backup);
                    break;
            }
        }

        private void BtnStopSingle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not BackupInfo backup) return;

            if (backup.State == "RUNNING" || backup.State == "PAUSED")
            {
                SendCommandForBackup("STOP", backup);
            }
        }

        private void BtnDeleteSingle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not BackupInfo backup) return;

            var result = MessageBox.Show($"Are you sure you want to delete the backup '{backup.Name}'?",
                                        "Delete Backup", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SendCommandForBackup("DELETE", backup);
            }
        }
        private async void BtnExecuteAll_Click(object sender, RoutedEventArgs e)
        {
            if (writer == null || client == null || !client.Connected)
            {
                MessageBox.Show("Not connected to server.");
                return;
            }

            if (backups.Count == 0)
            {
                MessageBox.Show("No backups available.");
                return;
            }

            var result = MessageBox.Show("Are you sure you want to start all backups?", "Execute All",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Envoyer la commande START_ALL au serveur
                    await writer.WriteLineAsync("START_ALL|ALL");

                    // Mettre à jour visuellement l'état des backups
                    foreach (var backup in backups)
                    {
                        backup.State = "PENDING...";
                    }

                    MessageBox.Show("Execute All command sent to server.", "Command Sent",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to send Execute All command: " + ex.Message);
                }
            }
        }

        private void BtnQuit_Click(object sender, RoutedEventArgs e)
        {
            DisconnectFromServer();
            Application.Current.Shutdown();
        }

        private void DisconnectFromServer()
        {
            cts?.Cancel();
            writer?.Close();
            reader?.Close();
            client?.Close();
            client = null;

            // Effacer la liste des sauvegardes
            Dispatcher.Invoke(() => backups.Clear());
        }
    }

    public class BackupInfo : INotifyPropertyChanged
    {
        private string? name;
        public string Name
        {
            get => name ?? string.Empty;
            set
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        private string? type;
        public string Type
        {
            get => type ?? string.Empty;
            set
            {
                type = value;
                OnPropertyChanged(nameof(Type));
            }
        }

        private string? source;
        public string Source
        {
            get => source ?? string.Empty;
            set
            {
                source = value;
                OnPropertyChanged(nameof(Source));
            }
        }

        private string? target;
        public string Target
        {
            get => target ?? string.Empty;
            set
            {
                target = value;
                OnPropertyChanged(nameof(Target));
            }
        }

        private int progress;
        public int Progress
        {
            get => progress;
            set
            {
                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private string? state;
        public string State
        {
            get => state ?? "READY";
            set
            {
                state = value;
                OnPropertyChanged(nameof(State));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
