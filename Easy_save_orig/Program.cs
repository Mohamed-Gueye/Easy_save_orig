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
        // Check if the application should use the WPF UI or console UI
        bool useWpf = true; // You can add a config option or command-line argument to switch between UI modes
        
        if (useWpf)
        {
            // Start the WPF application
            var application = new Application();
            application.StartupUri = new Uri("View/MainWindow.xaml", UriKind.Relative);
            application.Run();
        }
        else
        {
            // Fall back to the console UI
            var translation = new TranslationProcess();
            var backup = new BackupProcess();
            var app = new Main(translation, backup);
            app.Run();
        }
    }
}
