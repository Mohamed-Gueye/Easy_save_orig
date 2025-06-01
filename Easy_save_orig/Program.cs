using System;
using System.Windows;
using Easy_Save.Controller;
using Easy_Save.View;
using System.Threading;

class Program
{
    private static Mutex _mutex;
    private static bool _hasHandle = false;

    [STAThread]
    static void Main()
    {
        const string appName = "EasySaveApplication";

        try
        {
            _mutex = new Mutex(true, appName, out _hasHandle);

            if (!_hasHandle)
            {
                MessageBox.Show("Une instance de l'application est déjà en cours d'exécution.", "Easy Save", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool useWpf = true;

            if (useWpf)
            {
                var application = new Application();
                application.StartupUri = new Uri("View/MainWindow.xaml", UriKind.Relative);
                application.Exit += Application_Exit;
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
            ReleaseMutexSafely();
        }
    }

    private static void Application_Exit(object sender, ExitEventArgs e)
    {
        ReleaseMutexSafely();
    }

    private static void ReleaseMutexSafely()
    {
        if (_mutex != null)
        {
            try
            {
                if (_hasHandle)
                {
                    _mutex.ReleaseMutex();
                    _hasHandle = false;
                }
            }
            catch (ObjectDisposedException)
            {
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }
}
