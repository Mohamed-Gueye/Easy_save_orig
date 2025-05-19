using System;
using System.Collections.Generic;
using Easy_Save.Controller;
using Easy_Save.Model;

namespace Easy_Save.View
{
    public class Main
    {
        private readonly TranslationManager translationManager;
        private readonly BackupProcess backupProcess;

        public Main(BackupProcess backup)
        // In: backup (BackupProcess)
        // Out: /
        // Description: Initializes the Main class with translation manager and backup process.
        {
            translationManager = TranslationManager.Instance;
            backupProcess = backup;
        }

        public void Run()
        // Out: void
        // Description: Main execution loop displaying menu and handling user input.
        {
            ChooseLanguage();

            bool running = true;

            while (running)
            {
                DisplayMenu();
                Console.Write(translationManager.GetUITranslation("menu.prompt"));
                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        CreateBackup();
                        break;

                    case "2":
                        DeleteBackup();
                        break;

                    case "3":
                        ExecuteBackup();
                        break;

                    case "4":
                        ListBackups();
                        break;

                    case "5":
                        ExecuteAllBackups();
                        break;

                    case "6":
                        running = false;
                        break;

                    default:
                        Console.WriteLine(translationManager.GetUITranslation("menu.invalid"));
                        break;
                }

                Console.WriteLine(translationManager.GetUITranslation("menu.return"));
                Console.ReadKey();
            }
        }

        private void ChooseLanguage()
        // Out: void
        // Description: Prompts the user to choose a language and applies it.
        {
            Console.WriteLine("1. English");
            Console.WriteLine("2. Français");
            Console.Write(">> ");
            string? lang = Console.ReadLine();

            if (lang == "2")
                translationManager.LoadTranslation("fr");
            else
                translationManager.LoadTranslation("en");
        }

        private void DisplayMenu()
        // Out: void
        // Description: Clears the screen and displays the main menu with translated options.
        {
            Console.Clear();
            Console.WriteLine(translationManager.GetUITranslation("menu.title"));
            Console.WriteLine("1. " + translationManager.GetUITranslation("menu.create_backup"));
            Console.WriteLine("2. " + translationManager.GetUITranslation("menu.delete_backup"));
            Console.WriteLine("3. " + translationManager.GetUITranslation("menu.execute_backup"));
            Console.WriteLine("4. " + translationManager.GetUITranslation("menu.show_saves"));
            Console.WriteLine("5. " + translationManager.GetUITranslation("menu.execute_all_backups"));
            Console.WriteLine("6. " + translationManager.GetUITranslation("menu.exit"));
        }

        private void CreateBackup()
        // Out: void
        // Description: Prompts user for backup details and creates a new backup.
        {
            Console.WriteLine(translationManager.GetUITranslation("ask.name"));
            string name = Console.ReadLine() ?? "";

            Console.WriteLine(translationManager.GetUITranslation("ask.source"));
            string source = Console.ReadLine() ?? "";

            Console.WriteLine(translationManager.GetUITranslation("ask.target"));
            string target = Console.ReadLine() ?? "";

            Console.WriteLine(translationManager.GetUITranslation("ask.type"));
            string type = Console.ReadLine()?.Trim().ToLower() ?? "full";

            if (type != "full" && type != "differential")
            {
                Console.WriteLine("Type inconnu, utilisation de 'full' par défaut.");
                type = "full";
            }

            backupProcess.CreateBackup(name, source, target, type);
        }

        private void DeleteBackup()
        // Out: void
        // Description: Prompts for backup name and deletes it.
        {
            ListBackups();
            Console.WriteLine(translationManager.GetUITranslation("ask.name"));
            string name = Console.ReadLine() ?? "";
            backupProcess.DeleteBackup(name);
        }

        private void ExecuteBackup()
        // Out: void
        // Description: Prompts for backup name and executes the selected backup.
        {
            ListBackups();
            Console.WriteLine(translationManager.GetUITranslation("ask.name"));
            string name = Console.ReadLine() ?? "";
            backupProcess.ExecuteBackup(name);
        }

        private void ListBackups()
        // Out: void
        // Description: Displays all existing backups.
        {
            List<Backup> backups = backupProcess.GetAllBackup();
            if (backups.Count == 0)
            {
                Console.WriteLine(translationManager.GetUITranslation("no.backup"));
                return;
            }

            foreach (var backup in backups)
            {
                Console.WriteLine($" {backup.Name} | {backup.Type} | {backup.SourceDirectory} -> {backup.TargetDirectory}");
            }
        }

        private void ExecuteAllBackups()
        // Out: void
        // Description: Prompts for execution mode and runs all backups accordingly.
        {
            List<Backup> allBackups = backupProcess.GetAllBackup();
            if (allBackups.Count == 0)
            {
                Console.WriteLine(translationManager.GetUITranslation("no.backup"));
                return;
            }

            Console.WriteLine("1. " + translationManager.GetUITranslation("execution.linear"));
            Console.WriteLine("2. " + translationManager.GetUITranslation("execution.concurrent"));
            Console.Write(">> ");
            string? mode = Console.ReadLine();
            bool isConcurrent = mode == "2";

            backupProcess.RunAllBackups(isConcurrent);
            Console.WriteLine($"{allBackups.Count} {translationManager.GetUITranslation("backups.executed")}");
        }

    }
}