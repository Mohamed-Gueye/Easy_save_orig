using System;
using CryptoSoft;

namespace Easy_Save.Model
{
    public static class EncryptionHelper
    {
        // In: 
        //   - sourceFilePath (string): path to the file that needs to be encrypted
        //   - key (string): encryption key to use
        //   - cryptoSoftPath (string): unused in current implementation (legacy param)
        // Out: int (result code from CryptoService; <0 if error)
        // Description: Uses the CryptoSoft library to encrypt the specified file with a key. Instead of launching an external process, it calls CryptoService directly.
        public static int EncryptFile(string sourceFilePath, string key, string cryptoSoftPath)
        {
            Console.WriteLine($"[CryptoSoft] Encrypting: {sourceFilePath} with key: {key}");

            try
            {
                // Using direct method call instead of shelling out to an executable (better performance and control)
                int result = CryptoService.EncryptFile(sourceFilePath, key);
                Console.WriteLine($"[CryptoSoft] Result = {result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CryptoSoft] Error: {ex.Message}");
                return -99; // Custom code to indicate unexpected failure
            }
        }
    }
}
