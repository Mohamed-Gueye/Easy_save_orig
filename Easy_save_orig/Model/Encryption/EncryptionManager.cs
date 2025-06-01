using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace Easy_Save.Model
{
    public class EncryptionManager
    {
        public List<string> ExtensionsToEncrypt { get; set; } = new();
        public string EncryptionExecutablePath { get; set; } = string.Empty;
        public string EncryptionKey { get; set; } = string.Empty;
        public bool EncryptionEnabled { get; set; } = false;

        private static EncryptionManager? _instance;
        private static readonly object _lockObject = new object();

        // In: none
        // Out: EncryptionManager
        // Description: Thread-safe singleton accessor using lazy initialization. Ensures there's only one global instance.
        public static EncryptionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        // In: none
        // Out: EncryptionManager
        // Description: Loads settings from config.json. Supports both legacy flat format and newer "EncryptionSettings" block.
        public static EncryptionManager Load()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                    return new EncryptionManager();

                string jsonContent = File.ReadAllText(configPath);
                JsonDocument doc = JsonDocument.Parse(jsonContent);

                var settings = new EncryptionManager();

                // Legacy support: flat JSON properties
                if (doc.RootElement.TryGetProperty("extensionsToEncrypt", out var extsElement))
                {
                    settings.ExtensionsToEncrypt = extsElement.EnumerateArray()
                        .Select(ext => ext.GetString() ?? "")
                        .ToList();
                }

                if (doc.RootElement.TryGetProperty("encryptionExecutablePath", out var exePath))
                    settings.EncryptionExecutablePath = exePath.GetString() ?? "";

                if (doc.RootElement.TryGetProperty("key", out var keyElement))
                    settings.EncryptionKey = keyElement.GetString() ?? "";

                // Modern format: nested EncryptionSettings block
                if (doc.RootElement.TryGetProperty("EncryptionSettings", out var encryptionElement))
                {
                    if (encryptionElement.TryGetProperty("EncryptionEnabled", out var enabledElement))
                        settings.EncryptionEnabled = enabledElement.GetBoolean();

                    if (encryptionElement.TryGetProperty("EncryptExtensions", out var extensionsElement))
                    {
                        settings.ExtensionsToEncrypt = extensionsElement.EnumerateArray()
                            .Select(ext => ext.GetString() ?? "")
                            .ToList();
                    }

                    if (encryptionElement.TryGetProperty("EncryptionKey", out var encKeyElement))
                        settings.EncryptionKey = encKeyElement.GetString() ?? "";
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading encryption settings: {ex.Message}");
                return new EncryptionManager();
            }
        }

        // In: path (string)
        // Out: EncryptionManager
        // Description: Loads encryption settings from a specific file path (used for testing or alternate profiles).
        public static EncryptionManager Load(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                Console.WriteLine($"[DEBUG] Loading encryption config: {json}");
                var settings = JsonSerializer.Deserialize<EncryptionManager>(json);
                return settings ?? new EncryptionManager();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading encryption settings from {path}: {ex.Message}");
                return new EncryptionManager();
            }
        }

        // In: none
        // Out: void
        // Description: Saves encryption settings to config.json, supporting both modern and legacy formats for compatibility.
        public void Save()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

                JsonDocument existingDoc = File.Exists(configPath)
                    ? JsonDocument.Parse(File.ReadAllText(configPath))
                    : JsonDocument.Parse("{}");

                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();

                    // Preserve unrelated fields from existing config
                    foreach (var property in existingDoc.RootElement.EnumerateObject())
                    {
                        if (property.Name != "EncryptionSettings" &&
                            property.Name != "extensionsToEncrypt" &&
                            property.Name != "encryptionExecutablePath" &&
                            property.Name != "key")
                        {
                            property.WriteTo(writer);
                        }
                    }

                    // New format block
                    writer.WritePropertyName("EncryptionSettings");
                    writer.WriteStartObject();
                    writer.WriteBoolean("EncryptionEnabled", EncryptionEnabled);

                    writer.WritePropertyName("EncryptExtensions");
                    writer.WriteStartArray();
                    foreach (var ext in ExtensionsToEncrypt)
                        writer.WriteStringValue(ext);
                    writer.WriteEndArray();

                    writer.WriteString("EncryptionKey", EncryptionKey);
                    writer.WriteEndObject();

                    // Legacy format (backward compatibility)
                    writer.WritePropertyName("extensionsToEncrypt");
                    writer.WriteStartArray();
                    foreach (var ext in ExtensionsToEncrypt)
                        writer.WriteStringValue(ext);
                    writer.WriteEndArray();

                    writer.WriteString("encryptionExecutablePath", EncryptionExecutablePath);
                    writer.WriteString("key", EncryptionKey);

                    writer.WriteEndObject();
                }

                File.WriteAllText(configPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving encryption settings: {ex.Message}");
            }
        }

        // In: filePath (string)
        // Out: bool
        // Description: Returns true if the file extension matches a known encryptable extension and encryption is enabled.
        public bool ShouldEncryptFile(string filePath)
        {
            if (!EncryptionEnabled || string.IsNullOrEmpty(filePath))
                return false;

            string ext = Path.GetExtension(filePath).ToLower();
            return ExtensionsToEncrypt.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        // In: filePath (string)
        // Out: int
        // Description: Encrypts the given file using current settings. Returns result code from helper.
        public int EncryptFile(string filePath)
        {
            if (string.IsNullOrEmpty(EncryptionExecutablePath) || string.IsNullOrEmpty(EncryptionKey))
                return -1;

            return EncryptionHelper.EncryptFile(filePath, EncryptionKey, EncryptionExecutablePath);
        }
    }
}
