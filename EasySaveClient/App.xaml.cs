using System;
using System.Threading;
using System.Windows;


namespace EasySaveClient
{
    public partial class App : Application
    {
        private Mutex? mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            mutex = new Mutex(true, "EasySaveAppInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("L'application est déjà en cours d'exécution.");
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }
    }
}
