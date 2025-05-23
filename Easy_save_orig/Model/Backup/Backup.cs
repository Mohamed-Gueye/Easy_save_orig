using System;
using System.Threading;
using Easy_Save.Model.Enum;

namespace Easy_Save.Model;
public class Backup
{
    public string? Name { get; set; }
    public string SourceDirectory { get; set; }
    public string TargetDirectory { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Progress { get; set; } = "0%";
    
    // State management
    private BackupJobState _state = BackupJobState.READY;
    public BackupJobState State 
    { 
        get => _state; 
        set => _state = value; 
    }
    
    // Controls for pause/resume
    private CancellationTokenSource? _cancellationTokenSource;
    private ManualResetEventSlim? _pauseEvent;
    
    public CancellationToken CancellationToken => 
        (_cancellationTokenSource ?? (_cancellationTokenSource = new CancellationTokenSource())).Token;
    
    public Backup()
    {
        _pauseEvent = new ManualResetEventSlim(true); // Not paused initially
        _cancellationTokenSource = new CancellationTokenSource();
    }
    
    /// <summary>
    /// Start or resume the backup job
    /// </summary>
    public void Play()
    {
        if (_state == BackupJobState.PAUSED)
        {
            _pauseEvent?.Set(); // Resume execution
            _state = BackupJobState.RUNNING;
        }
        else if (_state == BackupJobState.READY || _state == BackupJobState.STOPPED)
        {
            // Reset if needed
            Reset();
            _state = BackupJobState.RUNNING;
        }
    }
    
    /// <summary>
    /// Pause the backup job (after current file completes)
    /// </summary>
    public void Pause()
    {
        if (_state == BackupJobState.RUNNING)
        {
            _pauseEvent?.Reset(); // Pause execution
            _state = BackupJobState.PAUSED;
        }
    }
    
    /// <summary>
    /// Stop the backup job immediately
    /// </summary>
    public void Stop()
    {
        if (_state == BackupJobState.RUNNING || _state == BackupJobState.PAUSED)
        {
            _cancellationTokenSource?.Cancel();
            _pauseEvent?.Set(); // Prevent deadlock
            _state = BackupJobState.STOPPED;
        }
    }
    
    /// <summary>
    /// Check if the job is paused and should wait, or if it's been cancelled
    /// </summary>
    public void CheckPauseAndCancellation()
    {
        _pauseEvent?.Wait(); // Wait if paused
        _cancellationTokenSource?.Token.ThrowIfCancellationRequested();
    }
    
    /// <summary>
    /// Reset the job state for a new run
    /// </summary>
    public void Reset()
    {
        // Dispose and recreate cancellation token
        if (_cancellationTokenSource != null)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        // Reset pause event
        _pauseEvent?.Set();
        
        _state = BackupJobState.READY;
        Progress = "0%";
    }
}
