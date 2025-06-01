using System;
using System.Threading;

namespace Easy_Save.Model.Status
{
    // Description: Tracks the byte-level progress of a backup operation.
    // Notes: Thread-safe using lock; also provides update throttling via time interval.
    public class ByteProgressTracker
    {
        private long _totalBytes;
        private long _copiedBytes;
        private readonly object _lockObject = new object();
        private int _lastReportedPercentage = -1;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private const int UPDATE_INTERVAL_MS = 50; // Update throttling interval (ms)

        public long TotalBytes
        {
            get { lock (_lockObject) { return _totalBytes; } }
            private set { lock (_lockObject) { _totalBytes = value; } }
        }

        public long CopiedBytes
        {
            get { lock (_lockObject) { return _copiedBytes; } }
            private set { lock (_lockObject) { _copiedBytes = value; } }
        }

        public int ProgressPercentage
        {
            get
            {
                lock (_lockObject)
                {
                    return _totalBytes > 0 ? (int)Math.Round((_copiedBytes * 100.0) / _totalBytes) : 0;
                }
            }
        }

        // Out: Event (Action<int>)
        // Description: Raised when progress percentage changes or throttled update is due.
        public event Action<int>? ProgressChanged;

        public ByteProgressTracker(long totalBytes)
        // In: totalBytes (long)
        // Out: ByteProgressTracker instance
        // Description: Initializes a new tracker with total bytes to be copied.
        {
            TotalBytes = totalBytes;
            _copiedBytes = 0;
        }

        public void Reset(long totalBytes)
        // In: totalBytes (long)
        // Out: void
        // Description: Resets the tracker with new total bytes and zero progress.
        {
            lock (_lockObject)
            {
                TotalBytes = totalBytes;
                _copiedBytes = 0;
            }
            ProgressChanged?.Invoke(0);
        }

        public void AddCopiedBytes(long bytes)
        // In: bytes (long)
        // Out: void
        // Description: Adds bytes to the progress and triggers update if percentage or time interval has changed.
        {
            int newPercentage;
            bool shouldUpdate = false;
            DateTime now = DateTime.Now;

            lock (_lockObject)
            {
                _copiedBytes += bytes;
                newPercentage = ProgressPercentage;

                shouldUpdate = (newPercentage != _lastReportedPercentage) ||
                              ((now - _lastUpdateTime).TotalMilliseconds >= UPDATE_INTERVAL_MS);

                if (shouldUpdate)
                {
                    _lastReportedPercentage = newPercentage;
                    _lastUpdateTime = now;
                }
            }

            if (shouldUpdate)
            {
                ProgressChanged?.Invoke(newPercentage);
            }
        }

        public void SetCopiedBytes(long bytes)
        // In: bytes (long)
        // Out: void
        // Description: Sets the number of copied bytes and triggers a progress update if required.
        {
            int newPercentage;
            bool shouldUpdate = false;
            DateTime now = DateTime.Now;

            lock (_lockObject)
            {
                _copiedBytes = Math.Min(bytes, _totalBytes);
                newPercentage = ProgressPercentage;

                shouldUpdate = (newPercentage != _lastReportedPercentage) ||
                              ((now - _lastUpdateTime).TotalMilliseconds >= UPDATE_INTERVAL_MS);

                if (shouldUpdate)
                {
                    _lastReportedPercentage = newPercentage;
                    _lastUpdateTime = now;
                }
            }

            if (shouldUpdate)
            {
                ProgressChanged?.Invoke(newPercentage);
            }
        }
    }
}
