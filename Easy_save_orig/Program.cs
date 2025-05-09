using Easy_Save.Controller;
using Easy_Save.View;

class Program
{
    static void Main()
    {
        var translation = new TranslationProcess();
        var backup = new BackupProcess();
        var app = new Main(translation, backup);
        app.Run();
    }
}
