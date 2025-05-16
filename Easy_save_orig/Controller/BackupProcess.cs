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
        
        // Propriétés pour les paramètres métier
        public long MaxFileSize { get; set; } = 0;
        public string[] RestrictedExtensions { get; set; } = Array.Empty<string>();
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        public string CryptoSoftPath { get; set; } = string.Empty;

        public BackupProcess()
        {
            backupManager = new BackupManager();
            LoadBusinessSettings();
        }
        
        private void LoadBusinessSettings()
        {
            var settings = BusinessSettings.Instance;
            BusinessSoftwareList = new List<string>(settings.BusinessSoftwareList);
            MaxFileSize = settings.MaxFileSize;
            RestrictedExtensions = settings.RestrictedExtensions.ToArray();
            CryptoSoftPath = settings.CryptoSoftPath;
        }
        
        public bool AddBusinessSoftware(string softwareName)
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;
                
            // Utiliser la méthode de la classe BusinessSettings
            var settings = BusinessSettings.Instance;
            bool added = settings.AddBusinessSoftware(softwareName);
            
            if (added)
            {
                // Mettre à jour la liste locale
                BusinessSoftwareList = new List<string>(settings.BusinessSoftwareList);
            }
            
            return added;
        }
        
        public bool RemoveBusinessSoftware(string softwareName)
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;
                
            // Utiliser la méthode de la classe BusinessSettings
            var settings = BusinessSettings.Instance;
            bool removed = settings.RemoveBusinessSoftware(softwareName);
            
            if (removed)
            {
                // Mettre à jour la liste locale
                BusinessSoftwareList = new List<string>(settings.BusinessSoftwareList);
            }
            
            return removed;
        }
        
        public List<string> GetBusinessSoftwareList()
        {
            return new List<string>(BusinessSoftwareList);
        }

        public void CreateBackup(string name, string src, string dest, string type)
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
        {
            // Vérifier si un des logiciels métier est en cours d'exécution
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
        {
            backupManager.ExecuteAllBackups();
        }

        public void RunAllBackups(bool isConcurrent)
        {
            backupManager.ExecuteAllBackupsAsync(isConcurrent).Wait();
        }

        public List<Backup> GetAllBackup()
        {
            return backupManager.GetAllBackup();
        }

        public void DeleteBackup(string name)
        {
            backupManager.RemoveBackup(name);
            Console.WriteLine($"Backup deleted : {name}");
        }
    }
}