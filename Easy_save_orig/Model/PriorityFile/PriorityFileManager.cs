using System;

namespace Easy_Save.Model
{
    public class PriorityFileManager
    {
        private static readonly object _lock = new();
        private static PriorityFileManager? _instance;
        private int _priorityFilesRemaining = 0;

        public static PriorityFileManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new PriorityFileManager();
                }
            }
        }

        public void AddPriorityFiles(int count)
        {
            lock (_lock)
            {
                _priorityFilesRemaining += count;
            }
        }

        public void Decrement()
        {
            lock (_lock)
            {
                if (_priorityFilesRemaining > 0)
                    _priorityFilesRemaining--;
            }
        }

        public bool HasPendingPriorityFiles()
        {
            lock (_lock)
            {
                return _priorityFilesRemaining > 0;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _priorityFilesRemaining = 0;
            }
        }
    }
}
