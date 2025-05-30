using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Easy_Save.Model;

namespace Easy_Save.Controller
{
    public class BackupProcess
    {
        private readonly BackupManager backupManager;

        // Property to expose BackupManager for event subscription
        public BackupManager BackupManager => backupManager;

        public long MaxFileSize { get; set; } = 0;
        public string[] RestrictedExtensions { get; set; } = Array.Empty<string>();
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        public string CryptoSoftPath { get; set; } = string.Empty;

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

        public bool AddPriorityExtension(string extension)
        // In: extension (string)
        // Out: bool (true if extension added successfully)
        // Description: Adds a priority extension to the settings.
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            var settings = BackupRulesManager.Instance;
            return settings.AddPriorityExtension(extension);
        }

        public bool RemovePriorityExtension(string extension)
        // In: extension (string)
        // Out: bool (true if extension removed successfully)
        // Description: Removes a priority extension from the settings.
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            var settings = BackupRulesManager.Instance;
            return settings.RemovePriorityExtension(extension);
        }

        public List<string> GetPriorityExtensionsList()
        // Out: List<string> 
        // Description: Returns a copy of the current list of priority extensions.
        {
            var settings = BackupRulesManager.Instance;
            return new List<string>(settings.PriorityExtensions);
        }

        public bool UpdateBandwidthThreshold(long thresholdKB)
        // In: thresholdKB (long)
        // Out: bool (true if threshold updated successfully)
        // Description: Updates the bandwidth threshold for large files.
        {
            var settings = BackupRulesManager.Instance;
            return settings.UpdateLargeFileSizeThreshold(thresholdKB);
        }

        public long GetBandwidthThreshold()
        // Out: long
        // Description: Returns the current bandwidth threshold in KB.
        {
            var settings = BackupRulesManager.Instance;
            return settings.GetLargeFileSizeThreshold();
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

            backupManager.ExecuteBackup(name);
        }

        public void StopBackup(string name)
        // In: name (string)
        // Out: void
        // Description: Stops the specified backup if it's currently running.
        {
            backupManager.StopBackup(name);
        }

        public void RunAllBackups()
        // In: none
        // Out: void
        // Description: Executes all backups sequentially.
        {
            backupManager.ExecuteAllBackups();
        }

        public void RunAllBackups(bool isConcurrent)
        // In: isConcurrent (bool)
        // Out: void
        // Description: Executes all backups, either concurrently or sequentially based on the boolean.
        {
            backupManager.ExecuteAllBackupsAsync(isConcurrent).Wait();
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

            var allBackups = GetAllBackup();
            return allBackups.FirstOrDefault(b => b.Name == name);
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