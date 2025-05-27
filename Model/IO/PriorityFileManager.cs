using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Easy_Save.Model.IO
{
    public class PriorityFileManager
    {
        private static PriorityFileManager? _instance;
        private static readonly object _lockObject = new object();
        private readonly ConcurrentDictionary<string, List<string>> _priorityFilesByBackup;
        private readonly ConcurrentDictionary<string, bool> _backupProcessingStatus;
        private readonly ConcurrentDictionary<string, bool> _backupHasPriorityFiles;

        private PriorityFileManager()
        {
            _priorityFilesByBackup = new ConcurrentDictionary<string, List<string>>();
            _backupProcessingStatus = new ConcurrentDictionary<string, bool>();
            _backupHasPriorityFiles = new ConcurrentDictionary<string, bool>();
        }

        public static PriorityFileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new PriorityFileManager();
                    }
                }
                return _instance;
            }
        }

        public void RegisterPriorityFiles(string backupName, string sourceDirectory)
        {
            var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions;
            if (priorityExtensions == null || !priorityExtensions.Any())
            {
                _backupHasPriorityFiles.AddOrUpdate(backupName, false, (_, _) => false);
                return;
            }

            string[] allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            var priorityFiles = allFiles
                .Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            bool hasPriorityFiles = priorityFiles.Any();
            _backupHasPriorityFiles.AddOrUpdate(backupName, hasPriorityFiles, (_, _) => hasPriorityFiles);
            _priorityFilesByBackup.AddOrUpdate(backupName, priorityFiles, (_, _) => priorityFiles);
            _backupProcessingStatus.AddOrUpdate(backupName, false, (_, _) => false);

            // Si on trouve des fichiers prioritaires, on met en pause toutes les sauvegardes non prioritaires en cours
            if (hasPriorityFiles)
            {
                PauseNonPriorityBackups();
            }
        }

        private void PauseNonPriorityBackups()
        {
            foreach (var backup in _backupProcessingStatus.Keys)
            {
                if (!_backupHasPriorityFiles.GetValueOrDefault(backup, false))
                {
                    _backupProcessingStatus.AddOrUpdate(backup, false, (_, _) => false);
                }
            }
        }

        public bool HasPendingPriorityFiles()
        {
            return _priorityFilesByBackup.Any(kvp => 
                kvp.Value.Any() && !_backupProcessingStatus.GetValueOrDefault(kvp.Key));
        }

        public void MarkBackupAsProcessing(string backupName)
        {
            _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
        }

        public void RemoveBackup(string backupName)
        {
            _priorityFilesByBackup.TryRemove(backupName, out _);
            _backupProcessingStatus.TryRemove(backupName, out _);
            _backupHasPriorityFiles.TryRemove(backupName, out _);
        }

        public void Reset()
        {
            _priorityFilesByBackup.Clear();
            _backupProcessingStatus.Clear();
            _backupHasPriorityFiles.Clear();
        }

        public bool CanProcessNonPriorityFiles(string backupName)
        {
            // Si cette sauvegarde a des fichiers prioritaires, on la laisse continuer
            if (_backupHasPriorityFiles.GetValueOrDefault(backupName, false))
            {
                return true;
            }

            // Sinon, on vérifie s'il y a des fichiers prioritaires en attente dans d'autres sauvegardes
            return !HasPendingPriorityFiles();
        }

        public void CheckAndUpdatePriorityStatus(string backupName, string sourceDirectory)
        {
            var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions;
            if (priorityExtensions == null || !priorityExtensions.Any())
                return;

            string[] allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            var priorityFiles = allFiles
                .Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            bool hasPriorityFiles = priorityFiles.Any();
            _backupHasPriorityFiles.AddOrUpdate(backupName, hasPriorityFiles, (_, _) => hasPriorityFiles);
            _priorityFilesByBackup.AddOrUpdate(backupName, priorityFiles, (_, _) => priorityFiles);

            if (hasPriorityFiles)
            {
                PauseNonPriorityBackups();
            }
        }
    }
} 