using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Easy_Save.Model.IO
{
    /// <summary>
    /// File copy utility with real-time byte progress tracking
    /// </summary>
    public static class ProgressAwareFileCopy
    {
        private const int BufferSize = 2 * 1024; // 2KB buffer for more frequent updates

        /// <summary>
        /// Copy a file with real-time progress updates based on bytes copied (synchronous version)
        /// </summary>
        /// <param name="sourceFile">Source file path</param>
        /// <param name="destinationFile">Destination file path</param>
        /// <param name="progressCallback">Callback to report bytes copied</param>
        /// <returns>Total bytes copied</returns>
        public static long CopyFileWithProgress(
            string sourceFile,
            string destinationFile,
            Action<long> progressCallback)
        {
            using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
            using var destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan);

            // Check file size to determine if we should add delay for small files
            long fileSize = sourceStream.Length;
            const long TenMB = 10 * 1024 * 1024; // 10MB threshold

            var buffer = new byte[BufferSize];
            long totalBytesCopied = 0;
            int bytesRead;

            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                destinationStream.Write(buffer, 0, bytesRead);
                totalBytesCopied += bytesRead;

                // Report progress for each buffer written
                progressCallback?.Invoke(bytesRead);

                // Add delay only for files smaller than 10MB for better visibility
                if (fileSize < TenMB)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }

            destinationStream.Flush();
            return totalBytesCopied;
        }
    }
}
