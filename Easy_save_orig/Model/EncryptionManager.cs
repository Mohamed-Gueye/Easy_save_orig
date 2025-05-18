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

        public static EncryptionManager Instance
        // Out: EncryptionManager
        // Description: Singleton instance accessor with lazy loading and thread safety.
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

        public static EncryptionManager Load()
        // Out: EncryptionManager
        // Description: Loads encryption settings from the config.json file or returns defaults.
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                {
                    return new EncryptionManager();
                }

                string jsonContent = File.ReadAllText(configPath);
                JsonDocument doc = JsonDocument.Parse(jsonContent);

                var settings = new EncryptionManager();

                // Load from root level properties (legacy format)
                if (doc.RootElement.TryGetProperty("extensionsToEncrypt", out JsonElement extsElement))
                {
                    settings.ExtensionsToEncrypt = new List<string>();
                    foreach (var ext in extsElement.EnumerateArray())
                    {
                        settings.ExtensionsToEncrypt.Add(ext.GetString() ?? "");
                    }
                }

                if (doc.RootElement.TryGetProperty("encryptionExecutablePath", out JsonElement exePath))
                {
                    settings.EncryptionExecutablePath = exePath.GetString() ?? "";
                }

                if (doc.RootElement.TryGetProperty("key", out JsonElement keyElement))
                {
                    settings.EncryptionKey = keyElement.GetString() ?? "";
                }

                // Try to load from EncryptionSettings section (new format)
                if (doc.RootElement.TryGetProperty("EncryptionSettings", out JsonElement encryptionElement))
                {
                    if (encryptionElement.TryGetProperty("EncryptionEnabled", out JsonElement enabledElement))
                    {
                        settings.EncryptionEnabled = enabledElement.GetBoolean();
                    }

                    if (encryptionElement.TryGetProperty("EncryptExtensions", out JsonElement extensionsElement))
                    {
                        settings.ExtensionsToEncrypt = new List<string>();
                        foreach (var ext in extensionsElement.EnumerateArray())
                        {
                            settings.ExtensionsToEncrypt.Add(ext.GetString() ?? "");
                        }
                    }

                    if (encryptionElement.TryGetProperty("EncryptionKey", out JsonElement encKeyElement))
                    {
                        settings.EncryptionKey = encKeyElement.GetString() ?? "";
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading encryption settings: {ex.Message}");
                return new EncryptionManager();
            }
        }

        public static EncryptionManager Load(string path)
        // In: path (string)
        // Out: EncryptionManager
        // Description: Loads encryption settings from the specified JSON configuration file.
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

        public void Save()
        // Out: void
        // Description: Saves the current encryption settings to config.json.
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                
                JsonDocument existingDoc;
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    existingDoc = JsonDocument.Parse(jsonContent);
                }
                else
                {
                    existingDoc = JsonDocument.Parse("{}");
                }

                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();

                        using (JsonDocument doc = existingDoc)
                        {
                            foreach (var property in doc.RootElement.EnumerateObject())
                            {
                                if (property.Name != "EncryptionSettings" && 
                                    property.Name != "extensionsToEncrypt" && 
                                    property.Name != "encryptionExecutablePath" && 
                                    property.Name != "key")
                                {
                                    property.WriteTo(writer);
                                }
                            }
                        }

                        // Write new EncryptionSettings section
                        writer.WritePropertyName("EncryptionSettings");
                        writer.WriteStartObject();
                        
                        writer.WriteBoolean("EncryptionEnabled", EncryptionEnabled);
                        
                        writer.WritePropertyName("EncryptExtensions");
                        writer.WriteStartArray();
                        foreach (var ext in ExtensionsToEncrypt)
                        {
                            writer.WriteStringValue(ext);
                        }
                        writer.WriteEndArray();
                        
                        writer.WriteString("EncryptionKey", EncryptionKey);
                        
                        writer.WriteEndObject();

                        // Write legacy format for backward compatibility
                        writer.WritePropertyName("extensionsToEncrypt");
                        writer.WriteStartArray();
                        foreach (var ext in ExtensionsToEncrypt)
                        {
                            writer.WriteStringValue(ext);
                        }
                        writer.WriteEndArray();
                        
                        writer.WriteString("encryptionExecutablePath", EncryptionExecutablePath);
                        writer.WriteString("key", EncryptionKey);
                        
                        writer.WriteEndObject();
                    }

                    var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                    File.WriteAllText(configPath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving encryption settings: {ex.Message}");
            }
        }

        public bool ShouldEncryptFile(string filePath)
        // In: filePath (string)
        // Out: bool
        // Description: Determines if a file should be encrypted based on its extension.
        {
            if (!EncryptionEnabled || string.IsNullOrEmpty(filePath))
                return false;
                
            string ext = Path.GetExtension(filePath).ToLower();
            return ExtensionsToEncrypt.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        public int EncryptFile(string filePath)
        // In: filePath (string)
        // Out: int
        // Description: Encrypts the specified file using the configured settings.
        {
            if (string.IsNullOrEmpty(EncryptionExecutablePath) || string.IsNullOrEmpty(EncryptionKey))
                return -1;
                
            return EncryptionHelper.EncryptFile(filePath, EncryptionKey, EncryptionExecutablePath);
        }
    }
}
