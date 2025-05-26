using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
namespace Easy_Save.Model.BackupServer
{
       public class BackupServer
        {
            private readonly List<Backup> _jobs;

            public BackupServer(List<Backup> jobs)
            {
                _jobs = jobs;
            }

            public void Start()
            {
                Task.Run(() => ListenForCommands());
                Task.Run(() => BroadcastProgress());
            }

            private void ListenForCommands()
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 12345);
                listener.Start();

                while (true)
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    string command = Encoding.UTF8.GetString(buffer, 0, bytes).ToUpper();

                    Console.WriteLine($"[COMMAND RECEIVED] {command}");

                    foreach (var job in _jobs)
                    {
                        switch (command)
                        {
                            case "PAUSE": job.Pause(); break;
                            case "PLAY": job.Play(); break;
                            case "STOP": job.Stop(); break;
                        }
                    }
                }
            }

            private void BroadcastProgress()
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 12346);
                listener.Start();

                while (true)
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    using NetworkStream stream = client.GetStream();

                    foreach (var job in _jobs)
                    {
                        string update = $"{job.Name}|{job.Progress}|{job.State}";
                        byte[] data = Encoding.UTF8.GetBytes(update);
                        stream.Write(data, 0, data.Length);
                    }

                    System.Threading.Thread.Sleep(1000); // Limit refresh rate
                }
            }
        }
    }


