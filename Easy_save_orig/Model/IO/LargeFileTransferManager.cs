using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Easy_Save.Model.IO
{
    /// <summary>
    /// Singleton manager used to limit the number of concurrent large file transfers.
    /// This helps prevent system overload or I/O bottlenecks when handling huge files.
    /// </summary>
    public class LargeFileTransferManager
    {
        private static LargeFileTransferManager? _instance;
        private static readonly object _lockObject = new object();

        // Only 1 large file transfer allowed at a time (semaphore = 1)
        private readonly SemaphoreSlim _largeFileTransferSemaphore;

        private LargeFileTransferManager()
        {
            _largeFileTransferSemaphore = new SemaphoreSlim(1, 1);
        }

        // In: none
        // Out: LargeFileTransferManager
        // Description: Thread-safe lazy singleton accessor.
        public static LargeFileTransferManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new LargeFileTransferManager();
                    }
                }
                return _instance;
            }
        }

        // In: none
        // Out: Task
        // Description: Waits asynchronously until the large file transfer slot is available.
        public async Task WaitForLargeFileTransferAsync()
        {
            await _largeFileTransferSemaphore.WaitAsync();
        }

        // In: none
        // Out: void
        // Description: Releases the large file transfer slot so other transfers can proceed.
        public void ReleaseLargeFileTransfer()
        {
            _largeFileTransferSemaphore.Release();
        }

        // In: filePath (string), sizeThresholdKB (long)
        // Out: bool
        // Description: Returns true if the file exceeds the specified size threshold (in KB).
        public bool IsFileLarge(string filePath, long sizeThresholdKB)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length > (sizeThresholdKB * 1024);
        }
    }
}
