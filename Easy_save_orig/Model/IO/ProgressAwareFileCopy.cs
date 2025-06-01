using System;
using System.IO;
using System.Threading;

namespace Easy_Save.Model.IO
{
    /// <summary>
    /// Provides file copying functionality with byte-level progress reporting.
    /// </summary>
    public static class ProgressAwareFileCopy
    {
        // Small buffer (2KB) allows frequent progress updates during copy.
        private const int BufferSize = 2 * 1024;

        // In: sourceFile (string), destinationFile (string), progressCallback (Action<long>)
        // Out: long
        // Description: Copies a file from source to destination, reporting progress via a callback on each chunk copied.
        public static long CopyFileWithProgress(
            string sourceFile,
            string destinationFile,
            Action<long> progressCallback)
        {
            using var sourceStream = new FileStream(
                sourceFile, FileMode.Open, FileAccess.Read,
                FileShare.Read, BufferSize, FileOptions.SequentialScan);

            using var destinationStream = new FileStream(
                destinationFile, FileMode.Create, FileAccess.Write,
                FileShare.None, BufferSize, FileOptions.SequentialScan);

            long fileSize = sourceStream.Length;
            const long TenMB = 10 * 1024 * 1024;

            var buffer = new byte[BufferSize];
            long totalBytesCopied = 0;
            int bytesRead;

            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                destinationStream.Write(buffer, 0, bytesRead);
                totalBytesCopied += bytesRead;

                // Notifies how many bytes were just copied
                progressCallback?.Invoke(bytesRead);

                // Optional delay for smaller files to better visualize progress (mainly for UI feedback)
                if (fileSize < TenMB)
                {
                    Thread.Sleep(500); // Sleep 0.5s for visibility
                }
            }

            destinationStream.Flush();
            return totalBytesCopied;
        }
    }
}
