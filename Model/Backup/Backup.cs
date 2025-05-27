using System;
using System.Threading;
using Easy_Save.Model.Enum;

namespace Easy_Save.Model;
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
    private ManualResetEventSlim _pauseEvent;
    
    public CancellationToken CancellationToken => 
        (_cancellationTokenSource ?? (_cancellationTokenSource = new CancellationTokenSource())).Token;
    
    public Backup()
    {
        _pauseEvent = new ManualResetEventSlim(true);
        Reset();
    }
    
    /// <summary>
    /// Start or resume the backup job
    /// </summary>
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
    
    /// <summary>
    /// Pause the backup job (after current file completes)
    /// </summary>
    public void Pause()
    {
        if (_state == BackupJobState.RUNNING)
        {
            _pauseEvent.Reset(); 
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
            _pauseEvent.Set(); 
            _state = BackupJobState.STOPPED;
        }
    }
    
    /// <summary>
    /// Check if the job is paused and should wait, or if it's been cancelled
    /// </summary>
    public void CheckPauseAndCancellation()
    {
        if (IsCancelled)
            throw new OperationCanceledException();

        _pauseEvent.Wait(); 
        _cancellationTokenSource?.Token.ThrowIfCancellationRequested();
    }
    
    /// <summary>
    /// Reset the job state for a new run
    /// </summary>
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
    }

    public void Resume()
    {
        IsPaused = false;
        _pauseEvent.Set();
    }

    public void Cancel()
    {
        IsCancelled = true;
        Resume(); // Pour s'assurer que le thread n'est pas bloqué
    }
}
