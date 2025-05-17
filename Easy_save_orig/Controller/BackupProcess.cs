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
        
        public long MaxFileSize { get; set; } = 0;
        public string[] RestrictedExtensions { get; set; } = Array.Empty<string>();
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        public string CryptoSoftPath { get; set; } = string.Empty;

        public BackupProcess()
        // Description: Constructor and loads business settings.
        {
            backupManager = new BackupManager();
            LoadBusinessSettings();
        }
        
        private void LoadBusinessSettings()
        // Description: Loads the business settings into the local variables from the singleton instance.
        {
            var settings = BusinessSettings.Instance;
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
                
            var settings = BusinessSettings.Instance;
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
                
            var settings = BusinessSettings.Instance;
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
            var settings = BusinessSettings.Instance;
            if (settings.IsAnyBusinessSoftwareRunning())
            {
                string? runningSoftware = settings.GetRunningBusinessSoftware();
                Console.WriteLine($"The business software '{runningSoftware}' is running. Backup execution is blocked.");
                return;
            }
            
            backupManager.ExecuteBackup(name);
        }

        public void RunAllBackups()
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
        // Out: List<Backup> 
        // Description: Returns a list of all backups.
        {
            return backupManager.GetAllBackup();
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