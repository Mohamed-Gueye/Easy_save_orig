using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Easy_Save.Model.IO
{
    // Event arguments for priority status changes
    public class PriorityStatusChangedEventArgs : EventArgs
    {
        public string BackupName { get; }
        public bool CanProcess { get; }
        public PauseReason Reason { get; }

        public PriorityStatusChangedEventArgs(string backupName, bool canProcess, PauseReason reason = PauseReason.None)
        {
            BackupName = backupName;
            CanProcess = canProcess;
            Reason = reason;
        }
    }

    public enum PauseReason
    {
        None,
        PriorityFiles
    }

    public class PriorityFileManager
    {
        private static PriorityFileManager? _instance;
        private static readonly object _lockObject = new object();
        private readonly ConcurrentDictionary<string, List<string>> _priorityFilesByBackup;
        private readonly ConcurrentDictionary<string, bool> _backupProcessingStatus;
        private readonly ConcurrentDictionary<string, bool> _backupHasPriorityFiles;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _backupSemaphores;
        private readonly SemaphoreSlim _globalCoordinationSemaphore;

        // Events for UI notifications
        public event EventHandler<PriorityStatusChangedEventArgs>? PriorityStatusChanged;

        private PriorityFileManager()
        {
            _priorityFilesByBackup = new ConcurrentDictionary<string, List<string>>();
            _backupProcessingStatus = new ConcurrentDictionary<string, bool>();
            _backupHasPriorityFiles = new ConcurrentDictionary<string, bool>();
            _backupSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            _globalCoordinationSemaphore = new SemaphoreSlim(1, 1);
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
                _priorityFilesByBackup.AddOrUpdate(backupName, new List<string>(), (_, _) => new List<string>());
                GetOrCreateSemaphore(backupName);
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
            GetOrCreateSemaphore(backupName);

            // Notify about priority status change
            PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, hasPriorityFiles));

            // Si on trouve des fichiers prioritaires, on coordonne avec les autres sauvegardes
            if (hasPriorityFiles)
            {
                Task.Run(() => CoordinatePriorityExecution());
            }
        }

        private SemaphoreSlim GetOrCreateSemaphore(string backupName)
        {
            return _backupSemaphores.GetOrAdd(backupName, _ => new SemaphoreSlim(1, 1));
        }

        private async Task CoordinatePriorityExecution()
        {
            await _globalCoordinationSemaphore.WaitAsync();
            try
            {
                // Pause all non-priority backups
                var tasksToNotify = new List<Task>();

                foreach (var kvp in _backupHasPriorityFiles)
                {
                    string backupName = kvp.Key;
                    bool hasPriorityFiles = kvp.Value;

                    if (!hasPriorityFiles && _backupProcessingStatus.GetValueOrDefault(backupName, false))
                    {
                        // This backup should be paused
                        tasksToNotify.Add(NotifyBackupPause(backupName, PauseReason.PriorityFiles));
                    }
                }

                await Task.WhenAll(tasksToNotify);
            }
            finally
            {
                _globalCoordinationSemaphore.Release();
            }
        }

        private async Task NotifyBackupPause(string backupName, PauseReason reason)
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                _backupProcessingStatus.AddOrUpdate(backupName, false, (_, _) => false);
                PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, false, reason));
            }
            finally
            {
                semaphore.Release();
            }
        }
        public bool HasPendingPriorityFiles()
        {
            var result = _priorityFilesByBackup.Any(kvp =>
                kvp.Value.Any() && _backupHasPriorityFiles.GetValueOrDefault(kvp.Key, false));

            Console.WriteLine($"[DEBUG] HasPendingPriorityFiles: {result}");
            if (result)
            {
                var priorityBackups = _priorityFilesByBackup
                    .Where(kvp => kvp.Value.Any() && _backupHasPriorityFiles.GetValueOrDefault(kvp.Key, false))
                    .Select(kvp => kvp.Key);
                Console.WriteLine($"[DEBUG] Priority backups still running: {string.Join(", ", priorityBackups)}");
            }

            return result;
        }

        public async Task<bool> WaitForPriorityCoordinationAsync(string backupName)
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                // If this backup has priority files, it can proceed
                if (_backupHasPriorityFiles.GetValueOrDefault(backupName, false))
                {
                    _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
                    return true;
                }

                // If no priority files exist anywhere, non-priority backups can proceed
                if (!HasPendingPriorityFiles())
                {
                    _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
                    PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, true, PauseReason.None));
                    return true;
                }

                // This backup should wait
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task MarkBackupAsProcessingAsync(string backupName)
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void MarkBackupAsProcessing(string backupName)
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            semaphore.Wait();
            try
            {
                _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
            }
            finally
            {
                semaphore.Release();
            }
        }
        public async Task RemoveBackupAsync(string backupName)
        {
            // Check if the semaphore exists before trying to use it
            if (!_backupSemaphores.TryGetValue(backupName, out var semaphore))
            {
                // If no semaphore exists, just clean up the dictionaries
                _priorityFilesByBackup.TryRemove(backupName, out _);
                _backupProcessingStatus.TryRemove(backupName, out _);
                _backupHasPriorityFiles.TryRemove(backupName, out _);
                return;
            }

            await semaphore.WaitAsync();
            try
            {
                _priorityFilesByBackup.TryRemove(backupName, out _);
                _backupProcessingStatus.TryRemove(backupName, out _);
                _backupHasPriorityFiles.TryRemove(backupName, out _);
                // Remove and dispose the semaphore (but don't release it afterward)
                if (_backupSemaphores.TryRemove(backupName, out var removedSemaphore))
                {
                    // Release once before disposing to balance the WaitAsync call above
                    removedSemaphore.Release();
                    removedSemaphore.Dispose();
                }

                Console.WriteLine($"[DEBUG] Removed backup {backupName}, checking for waiting backups");
                // Check if we can resume other backups
                await CheckAndResumeWaitingBackupsAsync();

                // Force notify all waiting backups to check their status
                await ForceCheckAllBackupsAsync();
            }
            catch
            {
                // If an exception occurs and we still have the semaphore, release it
                if (_backupSemaphores.ContainsKey(backupName))
                {
                    semaphore.Release();
                }
                throw;
            }
        }
        public void RemoveBackup(string backupName)
        {
            // Check if the semaphore exists before trying to use it
            if (!_backupSemaphores.TryGetValue(backupName, out var semaphore))
            {
                // If no semaphore exists, just clean up the dictionaries
                _priorityFilesByBackup.TryRemove(backupName, out _);
                _backupProcessingStatus.TryRemove(backupName, out _);
                _backupHasPriorityFiles.TryRemove(backupName, out _);
                return;
            }

            semaphore.Wait();
            try
            {
                _priorityFilesByBackup.TryRemove(backupName, out _);
                _backupProcessingStatus.TryRemove(backupName, out _);
                _backupHasPriorityFiles.TryRemove(backupName, out _);
                // Remove and dispose the semaphore (but don't release it afterward)
                if (_backupSemaphores.TryRemove(backupName, out var removedSemaphore))
                {
                    // Release once before disposing to balance the Wait call above
                    removedSemaphore.Release();
                    removedSemaphore.Dispose();
                }

                Console.WriteLine($"[DEBUG] Removed backup {backupName} (sync), checking for waiting backups");
                // Check if we can resume other backups (synchronous version)
                Task.Run(async () =>
                {
                    await CheckAndResumeWaitingBackupsAsync();
                    await ForceCheckAllBackupsAsync();
                });
            }
            catch
            {
                // If an exception occurs and we still have the semaphore, release it
                if (_backupSemaphores.ContainsKey(backupName))
                {
                    semaphore.Release();
                }
                throw;
            }
        }
        private async Task CheckAndResumeWaitingBackupsAsync()
        {
            Console.WriteLine($"[DEBUG] CheckAndResumeWaitingBackupsAsync called");
            if (!HasPendingPriorityFiles())
            {
                Console.WriteLine($"[DEBUG] No pending priority files, resuming non-priority backups");
                // Resume all paused non-priority backups
                var resumeTasks = new List<Task>();

                foreach (var kvp in _backupProcessingStatus)
                {
                    string backupName = kvp.Key;
                    bool isProcessing = kvp.Value;

                    if (!isProcessing && !_backupHasPriorityFiles.GetValueOrDefault(backupName, false))
                    {
                        Console.WriteLine($"[DEBUG] Resuming non-priority backup: {backupName}");
                        resumeTasks.Add(NotifyBackupResume(backupName));
                    }
                }

                await Task.WhenAll(resumeTasks);
            }
            else
            {
                Console.WriteLine($"[DEBUG] Still have pending priority files, not resuming");
            }
        }

        private async Task NotifyBackupResume(string backupName)
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
                PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, true, PauseReason.None));
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task MarkPriorityFileProcessedAsync(string backupName, string filePath)
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                if (_priorityFilesByBackup.TryGetValue(backupName, out var priorityFiles))
                {
                    priorityFiles.Remove(filePath);

                    // If no more priority files in this backup, update status
                    if (!priorityFiles.Any())
                    {
                        _backupHasPriorityFiles.AddOrUpdate(backupName, false, (_, _) => false);
                        await CheckAndResumeWaitingBackupsAsync();
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public bool IsFilePriority(string filePath)
        {
            var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions;
            if (priorityExtensions == null || !priorityExtensions.Any())
                return false;

            string extension = Path.GetExtension(filePath).ToLower();
            return priorityExtensions.Contains(extension);
        }

        public void Reset()
        {
            _globalCoordinationSemaphore.Wait();
            try
            {
                _priorityFilesByBackup.Clear();
                _backupProcessingStatus.Clear();
                _backupHasPriorityFiles.Clear();

                // Dispose all semaphores
                foreach (var semaphore in _backupSemaphores.Values)
                {
                    semaphore.Dispose();
                }
                _backupSemaphores.Clear();
            }
            finally
            {
                _globalCoordinationSemaphore.Release();
            }
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

        public async Task CheckAndUpdatePriorityStatusAsync(string backupName, string sourceDirectory)
        {
            var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions;
            if (priorityExtensions == null || !priorityExtensions.Any())
                return;

            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                string[] allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
                var priorityFiles = allFiles
                    .Where(f => priorityExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                bool hasPriorityFiles = priorityFiles.Any();
                bool previousStatus = _backupHasPriorityFiles.GetValueOrDefault(backupName, false);

                _backupHasPriorityFiles.AddOrUpdate(backupName, hasPriorityFiles, (_, _) => hasPriorityFiles);
                _priorityFilesByBackup.AddOrUpdate(backupName, priorityFiles, (_, _) => priorityFiles);

                // Notify if status changed
                if (previousStatus != hasPriorityFiles)
                {
                    PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, hasPriorityFiles));
                }

                if (hasPriorityFiles)
                {
                    await CoordinatePriorityExecution();
                }
                else
                {
                    await CheckAndResumeWaitingBackupsAsync();
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void CheckAndUpdatePriorityStatus(string backupName, string sourceDirectory)
        {
            Task.Run(async () => await CheckAndUpdatePriorityStatusAsync(backupName, sourceDirectory));
        }

        private async Task ForceCheckAllBackupsAsync()
        {
            Console.WriteLine($"[DEBUG] ForceCheckAllBackupsAsync called");

            // Get all backups that are currently tracked
            var allBackupNames = _backupProcessingStatus.Keys.ToList();

            foreach (var backupName in allBackupNames)
            {
                // Check if this backup should be able to process non-priority files
                bool canProcess = CanProcessNonPriorityFiles(backupName);
                bool isCurrentlyProcessing = _backupProcessingStatus.GetValueOrDefault(backupName, false);
                bool hasPriorityFiles = _backupHasPriorityFiles.GetValueOrDefault(backupName, false);

                Console.WriteLine($"[DEBUG] Backup {backupName}: canProcess={canProcess}, isProcessing={isCurrentlyProcessing}, hasPriority={hasPriorityFiles}");

                // If backup can process but isn't currently processing and doesn't have priority files
                if (canProcess && !isCurrentlyProcessing && !hasPriorityFiles)
                {
                    Console.WriteLine($"[DEBUG] Force resuming backup: {backupName}");
                    await NotifyBackupResume(backupName);
                }
            }
        }
    }
}