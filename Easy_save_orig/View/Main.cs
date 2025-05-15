using System;
using System.Collections.Generic;
using Easy_Save.Controller;
using Easy_Save.Model;

namespace Easy_Save.View
{
    public class Main
    {
        private readonly TranslationProcess translationProcess;
        private readonly BackupProcess backupProcess;

        public Main(TranslationProcess translation, BackupProcess backup)
        {
            translationProcess = translation;
            backupProcess = backup;
        }

        public void Run()
        {
            ChooseLanguage();

            bool running = true;

            while (running)
            {
                DisplayMenu();
                Console.Write(translationProcess.GetTranslation("menu.prompt"));
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
                        Console.WriteLine(translationProcess.GetTranslation("menu.invalid"));
                        break;
                }

                Console.WriteLine(translationProcess.GetTranslation("menu.return"));
                Console.ReadKey();
            }
        }

        private void ChooseLanguage()
        {
            Console.WriteLine("1. English");
            Console.WriteLine("2. Français");
            Console.Write(">> ");
            string? lang = Console.ReadLine();

            if (lang == "2")
                translationProcess.LoadTranslation("fr");
            else
                translationProcess.LoadTranslation("en");
        }

        private void DisplayMenu()
        {
            Console.Clear();
            Console.WriteLine(translationProcess.GetTranslation("menu.title"));
            Console.WriteLine("1. " + translationProcess.GetTranslation("menu.create_backup"));
            Console.WriteLine("2. " + translationProcess.GetTranslation("menu.delete_backup"));
            Console.WriteLine("3. " + translationProcess.GetTranslation("menu.execute_backup"));
            Console.WriteLine("4. " + translationProcess.GetTranslation("menu.show_saves"));
            Console.WriteLine("5. " + translationProcess.GetTranslation("menu.execute_all_backups"));
            Console.WriteLine("6. " + translationProcess.GetTranslation("menu.exit"));
        }

        private void CreateBackup()
        {
            Console.WriteLine(translationProcess.GetTranslation("ask.name"));
            string name = Console.ReadLine() ?? "";

            Console.WriteLine(translationProcess.GetTranslation("ask.source"));
            string source = Console.ReadLine() ?? "";

            Console.WriteLine(translationProcess.GetTranslation("ask.target"));
            string target = Console.ReadLine() ?? "";

            Console.WriteLine(translationProcess.GetTranslation("ask.type"));
            string type = Console.ReadLine()?.Trim().ToLower() ?? "full";

            if (type != "full" && type != "differential")
            {
                Console.WriteLine("Type inconnu, utilisation de 'full' par défaut.");
                type = "full";
            }

            backupProcess.CreateBackup(name, source, target, type);
        }


        private void DeleteBackup()
        {
            ListBackups();
            Console.WriteLine(translationProcess.GetTranslation("ask.name"));
            string name = Console.ReadLine() ?? "";
            backupProcess.DeleteBackup(name);
        }

        private void ExecuteBackup()
        {
            ListBackups();
            Console.WriteLine(translationProcess.GetTranslation("ask.name"));
            string name = Console.ReadLine() ?? "";
            backupProcess.ExecuteBackup(name);
        }

        private void ListBackups()
        {
            List<Backup> backups = backupProcess.GetAllBackup();
            if (backups.Count == 0)
            {
                Console.WriteLine(translationProcess.GetTranslation("no.backup"));
                return;
            }

            foreach (var backup in backups)
            {
                Console.WriteLine($" {backup.Name} | {backup.Type} | {backup.SourceDirectory} -> {backup.TargetDirectory}");
            }
        }

        private void ExecuteAllBackups()
        {
            List<Backup> allBackups = backupProcess.GetAllBackup();
            if (allBackups.Count == 0)
            {
                Console.WriteLine(translationProcess.GetTranslation("no.backup"));
                return;
            }

            Console.WriteLine("1. " + translationProcess.GetTranslation("execution.linear"));
            Console.WriteLine("2. " + translationProcess.GetTranslation("execution.concurrent"));
            Console.Write(">> ");
            string? mode = Console.ReadLine();
            bool isConcurrent = mode == "2";

            backupProcess.RunAllBackups(isConcurrent);
            Console.WriteLine($"{allBackups.Count} {translationProcess.GetTranslation("backups.executed")}");
        }

    }
}