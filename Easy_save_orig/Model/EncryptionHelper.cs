using System.Diagnostics;
using System;


namespace Easy_Save.Model
{
    public static class EncryptionHelper
    {
        public static int EncryptFile(string sourceFilePath, string key, string cryptoSoftPath)
        // In: sourceFilePath (string), key (string), cryptoSoftPath (string)
        // Out: int
        // Description: Launches the external encryption process (CryptoSoft) on the given file with the specified key.
        {
            Console.WriteLine($"[CryptoSoft] Appel sur : {sourceFilePath} avec clé : {key}");

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = cryptoSoftPath,
                        Arguments = $"\"{sourceFilePath}\" \"{key}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };

                process.Start();
                process.WaitForExit();
                Console.WriteLine($"[CryptoSoft] ExitCode = {(int)process.ExitCode}");


                return process.ExitCode; 
            }
            catch
            {
                return -99;
            }
        }
    }
}
