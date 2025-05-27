using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Easy_Save.Model;
using Easy_Save.Model.Enum;

namespace EasySaveClient
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Backup> Jobs { get; set; } = new ObservableCollection<Backup>();

        private TcpClient? client;
        private NetworkStream? stream;
        public MainWindow()
        {
            InitializeComponent();
            LoadBackups();                // Charge les sauvegardes enregistrées
            StartListeningForUpdates();   // Reçoit les mises à jour du serveur
            this.DataContext = this;
        }

        private void LoadBackups()
        {
            var backupList = Backup.AllBackup;
            foreach (var backup in backupList)
                Jobs.Add((Backup)backup);
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (GetBackupFromSender(sender) is Backup backup)
                backup.Play();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (GetBackupFromSender(sender) is Backup backup)
                backup.Pause();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (GetBackupFromSender(sender) is Backup backup)
                backup.Stop();
        }

        private Backup? GetBackupFromSender(object sender)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Backup backup)
                return backup;
            return null;
        }

        private async void StartListeningForUpdates()
        {
            try
            {
                TcpClient progressClient = new TcpClient("127.0.0.1", 12346);
                NetworkStream stream = progressClient.GetStream();
                byte[] buffer = new byte[1024];

                while (true)
                {
                    int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string update = Encoding.UTF8.GetString(buffer, 0, bytes);
                    var parts = update.Split('|');
                    if (parts.Length == 3)
                    {
                        string jobName = parts[0];
                        int progress = int.Parse(parts[1]);
                        string status = parts[2];

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var job = Jobs.FirstOrDefault(j => j.Name == jobName);
                            if (job != null)
                            {
                                job.Progress = $"{progress}%";
                                job.State = status switch
                                {
                                    "Paused" => BackupJobState.PAUSED,
                                    "Running" => BackupJobState.RUNNING,
                                    "Stopped" => BackupJobState.STOPPED,
                                    _ => BackupJobState.READY
                                };
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de réception des mises à jour : " + ex.Message);
            }
        }
    }
}
