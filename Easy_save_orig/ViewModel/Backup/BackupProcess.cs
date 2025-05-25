using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Easy_Save.Model;

namespace Easy_Save.Controller
{
    public class BackupProcess
    {
        private readonly BackupManager backupManager;

        public long MaxFileSize { get; set; } = 0;
        public string[] RestrictedExtensions { get; set; } = Array.Empty<string>();
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        public string CryptoSoftPath { get; set; } = string.Empty;
        public List<string> PriorityExtensions { get; set; } = new(); // ✅ Ajout

        public BackupProcess()
        // In: none
        // Out: none
        // Description: Constructor and loads business settings.
        {
            backupManager = new BackupManager();
            LoadBackupRules();
        }

        private void LoadBackupRules()
        // In: none
        // Out: void
        // Description: Loads the backup rules into the local variables from the singleton instance.
        {
            var settings = BackupRulesManager.Instance;
            BusinessSoftwareList = new List<string>(settings.BusinessSoftwareList);
            MaxFileSize = settings.MaxFileSize;
            RestrictedExtensions = settings.RestrictedExtensions.ToArray();
            CryptoSoftPath = settings.CryptoSoftPath;

            if (settings.PriorityExtensions != null)
            {
                PriorityExtensions = new List<string>(settings.PriorityExtensions); // ✅ Lecture des extensions prioritaires
            }
        }

        public bool AddBusinessSoftware(string softwareName)
        // In: softwareName (string)
        // Out: bool (true if software added successfully)
        // Description: Adds a software package to the settings and updates the local list.
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;

            var settings = BackupRulesManager.Instance;
            bool added = settings.AddBusinessSoftware(softwareName);

            if (added)
            {
                BusinessSoftwareList = new List<string>(settings.BusinessSoftwareList);
            }

            return added;
        }

        public bool RemoveBusinessSoftware(string softwareName)
        // In: softwareName (string)
        // Out: bool (true if software removed successfully)
        // Description: Removes a softawre package from the settings and updates the local list.
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;

            var settings = BackupRulesManager.Instance;
            bool removed = settings.RemoveBusinessSoftware(softwareName);

            if (removed)
            {
                BusinessSoftwareList = new List<string>(settings.BusinessSoftwareList);
            }

            return removed;
        }

        public List<string> GetBusinessSoftwareList()
        // Out: List<string> 
        // Description: Returns a copy of the current list of software packages.
        {
            return new List<string>(BusinessSoftwareList);
        }

        public void CreateBackup(string name, string src, string dest, string type)
        // In: name (string), src (string), dest (string), type (string)
        // Out: void
        // Description: Creates a new backup with the specified parameters and adds it to the manager.
        {
            if (!Directory.Exists(src))
            {
                Console.WriteLine($"The folder \"{src}\" doesn't exist");
                return;
            }

            if (!Directory.Exists(dest))
            {
                try
                {
                    Directory.CreateDirectory(dest);
                    Console.WriteLine($"Folder \"{dest}\" created");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Impossible to create folder : {ex.Message}");
                    return;
                }
            }

            var backup = new Backup
            {
                Name = name,
                SourceDirectory = src,
                TargetDirectory = dest,
                Type = type,
                Progress = "0%"
            };

            try
            {
                backupManager.AddBackup(backup);
                Console.WriteLine($"Backup created : {name}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Impossible to create backup : {ex.Message}");
            }
        }

        public void ExecuteBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Executes the specified backup if no software packages is running.
        {
            var settings = BackupRulesManager.Instance;
            if (settings.IsAnyBusinessSoftwareRunning())
            {
                string? runningSoftware = settings.GetRunningBusinessSoftware();
                Console.WriteLine($"The business software '{runningSoftware}' is running. Backup execution is blocked.");
                return;
            }

            Task.Run(() => backupManager.ExecuteBackup(name));
        }

        public void RunAllBackups()
        // In: none
        // Out: void
        // Description: Executes all backups sequentially.
        {
            _ = backupManager.ExecuteAllBackupsAsync(false);
        }

        public void RunAllBackups(bool isConcurrent)
        // In: isConcurrent (bool)
        // Out: void
        // Description: Executes all backups, either concurrently or sequentially based on the boolean.
        {
            _ = backupManager.ExecuteAllBackupsAsync(isConcurrent);
        }

        public List<Backup> GetAllBackup()
        // In: none
        // Out: List<Backup> 
        // Description: Returns a list of all backups.
        {
            return backupManager.GetAllBackup();
        }

        public Backup? GetBackup(string name)
        // In: name (string)
        // Out: Backup?
        // Description: Returns a specific backup by its name or null if not found.
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return backupManager.GetAllBackup().FirstOrDefault(b => b.Name == name);
        }

        public void DeleteBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Deletes a backup by its name
        {
            backupManager.RemoveBackup(name);
            Console.WriteLine($"Backup deleted : {name}");
        }
    }
}
