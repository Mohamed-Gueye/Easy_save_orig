using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model
{
    public class EncryptionSettings
    {
        public List<string> extensionsToEncrypt { get; set; } = new();
        public string encryptionExecutablePath { get; set; } = string.Empty;
        public string key { get; set; } = string.Empty;

        public static EncryptionSettings Load(string path)
        // In: path (string)
        // Out: EncryptionSettings
        // Description: Loads encryption settings from the JSON configuration file.
        {
            var json = File.ReadAllText(path);
            Console.WriteLine($"[DEBUG] Chargement de la config de chiffrement : {json}");
            return JsonSerializer.Deserialize<EncryptionSettings>(json);
        }
    }
}
