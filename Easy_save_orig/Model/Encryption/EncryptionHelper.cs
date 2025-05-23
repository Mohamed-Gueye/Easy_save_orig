using System.Diagnostics;
using System;
using CryptoSoft;

namespace Easy_Save.Model
{
    public static class EncryptionHelper
    {
        public static int EncryptFile(string sourceFilePath, string key, string cryptoSoftPath)
        // In: sourceFilePath (string), key (string), cryptoSoftPath (string) - Noter que cryptoSoftPath n'est plus utilisé directement
        // Out: int
        // Description: Utilise la classe CryptoService pour chiffrer le fichier donné avec la clé spécifiée.
        {
            Console.WriteLine($"[CryptoSoft] Appel sur : {sourceFilePath} avec clé : {key}");

            try
            {
                // Utilise directement la classe CryptoService au lieu d'appeler un processus externe
                int result = CryptoService.EncryptFile(sourceFilePath, key);
                Console.WriteLine($"[CryptoSoft] Résultat = {result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CryptoSoft] Erreur : {ex.Message}");
                return -99;
            }
        }
    }
}
