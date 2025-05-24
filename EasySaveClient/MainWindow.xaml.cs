using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EasySave3Orig
{
    public partial class MainWindow : Window
    {
        private TcpClient? client;
        private NetworkStream? stream;
        public ObservableCollection<BackupJob> Jobs { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            BackupJobList.ItemsSource = Jobs;
            ConnectToServer();
            StartListeningForUpdates();
        }

        private void ConnectToServer()
        {
            try
            {
                client = new TcpClient("127.0.0.1", 12345);
                stream = client.GetStream();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de connexion : " + ex.Message);
            }
        }

        private void SendCommand(string command)
        {
            if (stream == null || !stream.CanWrite) return;
            var data = Encoding.UTF8.GetBytes(command);
            stream.Write(data, 0, data.Length);
        }

        private void Pause_Click(object sender, RoutedEventArgs e) => SendCommand("PAUSE");
        private void Play_Click(object sender, RoutedEventArgs e) => SendCommand("PLAY");
        private void Stop_Click(object sender, RoutedEventArgs e) => SendCommand("STOP");

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
                            var job = this.Jobs.FirstOrDefault(j => j.JobName == jobName);
                            if (job != null)
                            {
                                job.Progress = progress;
                                job.Status = status;
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

    public class BackupJob
    {
        public string? JobName { get; set; }
        public string? Status { get; set; }
        public int Progress { get; set; }
    }
}
