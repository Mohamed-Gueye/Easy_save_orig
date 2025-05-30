using System;
using System.Threading;

namespace Easy_Save.Model.Status
{
    /// <summary>
    /// Tracks byte-level progress for backup operations
    /// </summary>
    public class ByteProgressTracker
    {
        private long _totalBytes;
        private long _copiedBytes;
        private readonly object _lockObject = new object();
        private int _lastReportedPercentage = -1;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private const int UPDATE_INTERVAL_MS = 50; // Force update every 50ms

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

        public event Action<int>? ProgressChanged;

        public ByteProgressTracker(long totalBytes)
        {
            TotalBytes = totalBytes;
            _copiedBytes = 0;
        }

        public void Reset(long totalBytes)
        {
            lock (_lockObject)
            {
                TotalBytes = totalBytes;
                _copiedBytes = 0;
            }
            ProgressChanged?.Invoke(0);
        }
        public void AddCopiedBytes(long bytes)
        {
            int newPercentage;
            bool shouldUpdate = false;
            DateTime now = DateTime.Now;

            lock (_lockObject)
            {
                _copiedBytes += bytes;
                newPercentage = ProgressPercentage;

                // Update if percentage changed OR if enough time has passed
                shouldUpdate = (newPercentage != _lastReportedPercentage) ||
                              ((now - _lastUpdateTime).TotalMilliseconds >= UPDATE_INTERVAL_MS);

                if (shouldUpdate)
                {
                    _lastReportedPercentage = newPercentage;
                    _lastUpdateTime = now;
                }
            }

            // Trigger event if we should update
            if (shouldUpdate)
            {
                ProgressChanged?.Invoke(newPercentage);
            }
        }
        public void SetCopiedBytes(long bytes)
        {
            int newPercentage;
            bool shouldUpdate = false;
            DateTime now = DateTime.Now;

            lock (_lockObject)
            {
                _copiedBytes = Math.Min(bytes, _totalBytes);
                newPercentage = ProgressPercentage;

                // Update if percentage changed OR if enough time has passed
                shouldUpdate = (newPercentage != _lastReportedPercentage) ||
                              ((now - _lastUpdateTime).TotalMilliseconds >= UPDATE_INTERVAL_MS);

                if (shouldUpdate)
                {
                    _lastReportedPercentage = newPercentage;
                    _lastUpdateTime = now;
                }
            }

            // Trigger event if we should update
            if (shouldUpdate)
            {
                ProgressChanged?.Invoke(newPercentage);
            }
        }
    }
}
