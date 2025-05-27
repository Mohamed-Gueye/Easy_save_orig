using System;
using System.IO;

namespace CryptoSoft
{
    public class CryptoService
    {
        /// <summary>
        /// Encrypt a file using the XOR algorithm
        /// </summary>
        /// <param name="filePath">Path to the file to encrypt</param>
        /// <param name="key">Encryption key</param>
        /// <returns>Time taken to encrypt in milliseconds, or negative value if error</returns>
        public static int EncryptFile(string filePath, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(key))
                {
                    Console.WriteLine("Error: File path or key is empty");
                    return -1;
                }

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return -2;
                }

                var fileManager = new FileManager(filePath, key);
                return fileManager.Encrypt();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return -3;
            }
        }
    }
}
