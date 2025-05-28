using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Easy_Save.Interfaces;
using Easy_Save.Model;
using Easy_Save.Model.IO;
using Easy_Save.Model.Observer;

namespace Easy_Save.Strategies
{
    public abstract class BaseBackupStrategy : IBackupStrategy
    {
        protected readonly BackupRulesManager rulesManager;

        protected BaseBackupStrategy()
        {
            rulesManager = BackupRulesManager.Instance;
        }

        public abstract void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver);

        protected string[] GetSortedFilesByPriority(string sourceDirectory)
        {
            var allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            
            // Split files into priority and non-priority
            var priorityFiles = new List<string>();
            var nonPriorityFiles = new List<string>();

            foreach (var file in allFiles)
            {
                if (rulesManager.IsPriorityFile(file))
                {
                    priorityFiles.Add(file);
                }
                else
                {
                    nonPriorityFiles.Add(file);
                }
            }

            // Combine lists with priority files first
            return priorityFiles.Concat(nonPriorityFiles).ToArray();
        }

        protected bool HasPriorityFilesRemaining(string[] files, int currentIndex)
        {
            for (int i = currentIndex + 1; i < files.Length; i++)
            {
                if (rulesManager.IsPriorityFile(files[i]))
                {
                    return true;
                }
            }
            return false;
        }

        protected bool CanProcessNonPriorityFile(string[] files, int currentIndex)
        {
            if (rulesManager.IsPriorityFile(files[currentIndex]))
            {
                return true; // Priority files can always be processed
            }

            // Check if there are any priority files remaining
            return !HasPriorityFilesRemaining(files, currentIndex);
        }
    }
} 