using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Easy_Save.Model.IO
{
    // EventArgs subclass used for signaling backup priority status changes
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

    // Manages coordination between backup processes when priority files are detected
    public class PriorityFileManager
    {
        private static PriorityFileManager? _instance;
        private static readonly object _lockObject = new object();
        private readonly ConcurrentDictionary<string, List<string>> _priorityFilesByBackup;
        private readonly ConcurrentDictionary<string, bool> _backupProcessingStatus;
        private readonly ConcurrentDictionary<string, bool> _backupHasPriorityFiles;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _backupSemaphores;
        private readonly SemaphoreSlim _globalCoordinationSemaphore;

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
        // Out: PriorityFileManager
        // Description: Singleton accessor for PriorityFileManager
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
        // In: backupName (string), sourceDirectory (string)
        // Out: void
        // Description: Detects priority files in the source directory and registers them for coordination.
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

            PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, hasPriorityFiles));

            if (hasPriorityFiles)
            {
                Task.Run(() => CoordinatePriorityExecution());
            }
        }

        private SemaphoreSlim GetOrCreateSemaphore(string backupName)
        // In: backupName (string)
        // Out: SemaphoreSlim
        // Description: Ensures each backup has a dedicated semaphore for synchronization.
        {
            return _backupSemaphores.GetOrAdd(backupName, _ => new SemaphoreSlim(1, 1));
        }

        private async Task CoordinatePriorityExecution()
        // Out: Task
        // Description: Coordinates execution when multiple backups involve priority files using global semaphore.
        {
            await _globalCoordinationSemaphore.WaitAsync();
            try
            {
                var tasksToNotify = new List<Task>();

                foreach (var kvp in _backupHasPriorityFiles)
                {
                    string backupName = kvp.Key;
                    bool hasPriorityFiles = kvp.Value;

                    if (!hasPriorityFiles && _backupProcessingStatus.GetValueOrDefault(backupName, false))
                    {
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
        // In: backupName (string), reason (PauseReason)
        // Out: Task
        // Description: Notifies a specific backup to pause due to coordination constraints.
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
        // Out: bool
        // Description: Checks if any priority files are still pending in active backups.
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
        // In: backupName (string)
        // Out: Task<bool>
        // Description: Waits for permission to process backup depending on priority file conditions.
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                if (_backupHasPriorityFiles.GetValueOrDefault(backupName, false))
                {
                    _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
                    return true;
                }

                if (!HasPendingPriorityFiles())
                {
                    _backupProcessingStatus.AddOrUpdate(backupName, true, (_, _) => true);
                    PriorityStatusChanged?.Invoke(this, new PriorityStatusChangedEventArgs(backupName, true, PauseReason.None));
                    return true;
                }

                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task MarkBackupAsProcessingAsync(string backupName)
        // In: backupName (string)
        // Out: Task
        // Description: Marks a backup as actively processing.
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
        // In: backupName (string)
        // Out: void
        // Description: Synchronous version to mark a backup as processing.
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
        // In: backupName (string)
        // Out: Task
        // Description: Cleans up and removes all internal tracking for the given backup.
        {
            if (!_backupSemaphores.TryGetValue(backupName, out var semaphore))
            {
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
                if (_backupSemaphores.TryRemove(backupName, out var removedSemaphore))
                {
                    removedSemaphore.Release();
                    removedSemaphore.Dispose();
                }

                Console.WriteLine($"[DEBUG] Removed backup {backupName}, checking for waiting backups");
                await CheckAndResumeWaitingBackupsAsync();

                await ForceCheckAllBackupsAsync();
            }
            catch
            {
                if (_backupSemaphores.ContainsKey(backupName))
                {
                    semaphore.Release();
                }
                throw;
            }
        }
        public void RemoveBackup(string backupName)
        // In: backupName (string)
        // Out: void
        // Description: Synchronously removes all references and semaphores associated with a backup.
        // Notes: Uses a semaphore per backup to ensure safe concurrent access. Launches async follow-up to coordinate backups.
        {
            if (!_backupSemaphores.TryGetValue(backupName, out var semaphore))
            {
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
                if (_backupSemaphores.TryRemove(backupName, out var removedSemaphore))
                {
                    removedSemaphore.Release();
                    removedSemaphore.Dispose();
                }

                Console.WriteLine($"[DEBUG] Removed backup {backupName} (sync), checking for waiting backups");
                Task.Run(async () =>
                {
                    await CheckAndResumeWaitingBackupsAsync();
                    await ForceCheckAllBackupsAsync();
                });
            }
            catch
            {
                if (_backupSemaphores.ContainsKey(backupName))
                {
                    semaphore.Release();
                }
                throw;
            }
        }
        private async Task CheckAndResumeWaitingBackupsAsync()
        // Out: Task
        // Description: Checks if non-priority backups can be resumed and notifies them accordingly.
        // Notes: Called when priority files are processed or removed. Avoids race conditions via local locking.
        {
            Console.WriteLine($"[DEBUG] CheckAndResumeWaitingBackupsAsync called");
            if (!HasPendingPriorityFiles())
            {
                Console.WriteLine($"[DEBUG] No pending priority files, resuming non-priority backups");
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
        // In: backupName (string)
        // Out: Task
        // Description: Marks a backup as resumable and triggers a status event.
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
        // In: backupName (string), filePath (string)
        // Out: Task
        // Description: Removes a file from the list of priority files and triggers resume logic if none remain.
        {
            var semaphore = GetOrCreateSemaphore(backupName);
            await semaphore.WaitAsync();
            try
            {
                if (_priorityFilesByBackup.TryGetValue(backupName, out var priorityFiles))
                {
                    priorityFiles.Remove(filePath);

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
        // In: filePath (string)
        // Out: bool
        // Description: Checks if a given file has a priority extension based on current rules.
        {
            var priorityExtensions = BackupRulesManager.Instance.PriorityExtensions;
            if (priorityExtensions == null || !priorityExtensions.Any())
                return false;

            string extension = Path.GetExtension(filePath).ToLower();
            return priorityExtensions.Contains(extension);
        }

        public void Reset()
        // Out: void
        // Description: Clears all internal tracking and releases all semaphores.
        // Notes: Use with caution. Should be called only on full system reset.
        {
            _globalCoordinationSemaphore.Wait();
            try
            {
                _priorityFilesByBackup.Clear();
                _backupProcessingStatus.Clear();
                _backupHasPriorityFiles.Clear();

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
        // In: backupName (string)
        // Out: bool
        // Description: Returns true if a backup can process non-priority files, i.e., if no priority conflict exists.
        {
            if (_backupHasPriorityFiles.GetValueOrDefault(backupName, false))
            {
                return true;
            }

            return !HasPendingPriorityFiles();
        }

        public async Task CheckAndUpdatePriorityStatusAsync(string backupName, string sourceDirectory)
        // In: backupName (string), sourceDirectory (string)
        // Out: Task
        // Description: Scans directory for priority files and updates tracking state accordingly.
        // Notes: Uses per-backup semaphores to ensure thread-safe updates.
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
        // In: backupName (string), sourceDirectory (string)
        // Out: void
        // Description: Fire-and-forget wrapper around the async version of priority file check.
        // Notes: Runs async task in the background to avoid blocking the caller.
        {
            Task.Run(async () => await CheckAndUpdatePriorityStatusAsync(backupName, sourceDirectory));
        }

        private async Task ForceCheckAllBackupsAsync()
        // Out: Task
        // Description: Re-evaluates all backups and resumes them if conditions allow (no priority conflict).
        // Notes: Called after backup removal or completion.
        {
            Console.WriteLine($"[DEBUG] ForceCheckAllBackupsAsync called");

            var allBackupNames = _backupProcessingStatus.Keys.ToList();

            foreach (var backupName in allBackupNames)
            {
                bool canProcess = CanProcessNonPriorityFiles(backupName);
                bool isCurrentlyProcessing = _backupProcessingStatus.GetValueOrDefault(backupName, false);
                bool hasPriorityFiles = _backupHasPriorityFiles.GetValueOrDefault(backupName, false);

                Console.WriteLine($"[DEBUG] Backup {backupName}: canProcess={canProcess}, isProcessing={isCurrentlyProcessing}, hasPriority={hasPriorityFiles}");

                if (canProcess && !isCurrentlyProcessing && !hasPriorityFiles)
                {
                    Console.WriteLine($"[DEBUG] Force resuming backup: {backupName}");
                    await NotifyBackupResume(backupName);
                }
            }
        }
    }
}