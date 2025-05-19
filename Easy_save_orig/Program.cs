using System;
using System.Windows;
using Easy_Save.Controller;
using Easy_Save.View;
using System.Linq;

class Program
{
    [STAThread]
    static void Main()
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
}
