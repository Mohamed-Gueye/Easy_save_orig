using System;
using System.Windows;
using Easy_Save.Controller;
using Easy_Save.View;
using System.Linq;
using System.Threading;

class Program
{
    private static Mutex _mutex = null;
    
    [STAThread]
    static void Main()
    {
        const string appName = "EasySaveApplication";
        bool createdNew;

        _mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            MessageBox.Show("Une instance de l'application est déjà en cours d'exécution.", "EasySave", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            bool useWpf = true; 
            
            if (useWpf)
            {
                var application = new Application();
                application.StartupUri = new Uri("View/MainWindow.xaml", UriKind.Relative);
                application.Run();
            }
            else
            {
                var backup = new BackupProcess();
                var app = new Main(backup);
                app.Run();
            }
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }
}
