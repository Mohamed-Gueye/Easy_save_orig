using System;
using System.Threading;
using Easy_Save.Model.Enum;
using Easy_Save.Model.Status;

namespace Easy_Save.Model
{
    public class Backup
    {
        public string Name { get; set; }
        public string SourceDirectory { get; set; }
        public string TargetDirectory { get; set; }
        public string Type { get; set; }
        public string Progress { get; set; }
        public bool IsPaused { get; private set; }
        public bool IsCancelled { get; private set; }

        private BackupJobState _state = BackupJobState.READY;
        public BackupJobState State
        {
            get => _state;
            set => _state = value;
        }

        private CancellationTokenSource? _cancellationTokenSource;
        private ManualResetEventSlim _pauseEvent; // Used to pause/resume threads

        public ByteProgressTracker? ProgressTracker { get; private set; }

        public event Action<ByteProgressTracker>? ProgressTrackerInitialized;

        // Exposes the cancellation token for cooperative cancellation
        public CancellationToken CancellationToken =>
            (_cancellationTokenSource ?? (_cancellationTokenSource = new CancellationTokenSource())).Token;

        public Backup()
        {
            _pauseEvent = new ManualResetEventSlim(true);
            Reset();
        }

        // In: none
        // Out: void
        // Description: Starts or resumes the backup job depending on its current state.
        public void Play()
        {
            if (_state == BackupJobState.PAUSED)
            {
                _pauseEvent.Set();
                _state = BackupJobState.RUNNING;
            }
            else if (_state == BackupJobState.READY || _state == BackupJobState.STOPPED)
            {
                Reset();
                _state = BackupJobState.RUNNING;
            }
        }

        // In: none
        // Out: void
        // Description: Pauses the backup job by resetting the ManualResetEvent.
        public void Pause()
        {
            if (_state == BackupJobState.RUNNING)
            {
                _pauseEvent.Reset();
                _state = BackupJobState.PAUSED;
            }
        }

        // In: none
        // Out: void
        // Description: Stops the backup job and cancels the current operation.
        public void Stop()
        {
            if (_state == BackupJobState.RUNNING || _state == BackupJobState.PAUSED)
            {
                _cancellationTokenSource?.Cancel();
                _pauseEvent.Set(); // Ensure thread is released from wait state
                _state = BackupJobState.STOPPED;

                Progress = "0%";
                ProgressTracker?.Reset(ProgressTracker.TotalBytes);
            }
        }

        // In: none
        // Out: void
        // Description: Checks for pause or cancellation, blocks if paused, throws if cancelled.
        public void CheckPauseAndCancellation()
        {
            if (IsCancelled)
                throw new OperationCanceledException();

            _pauseEvent.Wait(); // Blocks thread if paused
            _cancellationTokenSource?.Token.ThrowIfCancellationRequested();
        }

        // In: none
        // Out: void
        // Description: Resets the job state to prepare for a new run.
        public void Reset()
        {
            if (_cancellationTokenSource != null)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            _pauseEvent.Set();
            _state = BackupJobState.READY;
            Progress = "0%";
            IsPaused = false;
            IsCancelled = false;

            ProgressTracker?.Reset(ProgressTracker.TotalBytes);
        }

        // In: none
        // Out: void
        // Description: Resumes a paused job by setting the pause event.
        public void Resume()
        {
            IsPaused = false;
            _pauseEvent.Set();
        }

        // In: none
        // Out: void
        // Description: Cancels the job and ensures the thread is not blocked on pause.
        public void Cancel()
        {
            IsCancelled = true;
            Resume(); // Unblocks thread if paused
        }

        // In: totalBytes (long)
        // Out: void
        // Description: Initializes the byte-level progress tracker and hooks the progress update event.
        public void InitializeProgressTracker(long totalBytes)
        {
            ProgressTracker = new ByteProgressTracker(totalBytes);
            ProgressTracker.ProgressChanged += (percentage) =>
            {
                Progress = $"{percentage}%";
            };

            ProgressTrackerInitialized?.Invoke(ProgressTracker);
        }
    }
}
