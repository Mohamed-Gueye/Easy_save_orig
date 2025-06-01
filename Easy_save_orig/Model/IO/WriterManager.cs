using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model.IO
{
    /// <summary>
    /// Handles writing and reading JSON files for configuration and data persistence.
    /// Implements a thread-safe singleton pattern.
    /// </summary>
    public class WriterManager
    {
        private static WriterManager? _instance;
        private static readonly object _lock = new(); // Used for thread-safe singleton access

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true // Makes JSON output easier to read
        };

        // In: none
        // Out: WriterManager
        // Description: Provides global access to the singleton instance in a thread-safe way.
        public static WriterManager Instance
        {
            get
            {
                lock (_lock) // Ensures only one thread initializes the instance
                {
                    return _instance ??= new WriterManager();
                }
            }
        }

        // Private constructor to prevent external instantiation
        private WriterManager() { }

        // In: data (T), path (string)
        // Out: void
        // Description: Serializes the given object to JSON and writes it to the specified file path.
        public void WriteJson<T>(T data, string path)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, _options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON write error: {ex.Message}");
            }
        }

        // In: path (string)
        // Out: T (nullable)
        // Description: Reads JSON from the given file and deserializes it into the specified type.
        public T? LoadJson<T>(string path)
        {
            try
            {
                if (!File.Exists(path)) return default;
                string content = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON read error: {ex.Message}");
                return default;
            }
        }

        // In: config (AppConfiguration)
        // Out: void
        // Description: Writes the application configuration object to "config.json".
        public void WriteLogConfig(AppConfiguration config)
        {
            WriteJson(config, "config.json");
        }
    }
}
