using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Easy_Save.Model.IO
{
    public class LargeFileTransferManager
    {
        private static LargeFileTransferManager? _instance;
        private static readonly object _lockObject = new object();
        private readonly SemaphoreSlim _largFileTransferSemaphore;
        
        private LargeFileTransferManager()
        {
            _largFileTransferSemaphore = new SemaphoreSlim(1, 1); 
        }

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

        public async Task WaitForLargeFileTransferAsync()
        {
            await _largFileTransferSemaphore.WaitAsync();
        }

        public void ReleaseLargeFileTransfer()
        {
            _largFileTransferSemaphore.Release();
        }

        public bool IsFileLarge(string filePath, long sizeThresholdKB)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length > (sizeThresholdKB * 1024);
        }
    }
} 